// <copyright file="TopUpController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.GameLogic.PlugIns.Escrow;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// Top-ups by bank transfer (the "Bank top-up" plug-in): what the web game's top-up page shows, and the
/// webhooks of Casso and SePay, which report incoming transfers. No login: the page is public, and the
/// webhooks prove themselves with the keys set in the plug-in, compared in constant time.
/// </summary>
[Route("api/topup/")]
[AllowAnonymous]
public class TopUpController : Controller
{
    private readonly IPersistenceContextProvider _contextProvider;
    private readonly BankTopUpService _topUps;
    private readonly ILogger<TopUpController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TopUpController"/> class.
    /// </summary>
    /// <param name="contextProvider">The persistence context provider.</param>
    /// <param name="topUps">The top-up service.</param>
    /// <param name="logger">The logger.</param>
    public TopUpController(IPersistenceContextProvider contextProvider, BankTopUpService topUps, ILogger<TopUpController> logger)
    {
        this._contextProvider = contextProvider;
        this._topUps = topUps;
        this._logger = logger;
    }

    /// <summary>
    /// What the top-up page shows: the bank accounts, the rate and the transfer word. Never the keys.
    /// </summary>
    /// <returns>The public top-up information; <c>enabled: false</c> when it's off.</returns>
    [HttpGet("info")]
    public async Task<IActionResult> InfoAsync()
    {
        this.Response.Headers.CacheControl = "no-cache";
        var config = await this.ReadAsync().ConfigureAwait(false);
        var accounts = config?.UsableAccounts() ?? [];
        if (config is null || accounts.Count == 0)
        {
            return this.Json(new { enabled = false });
        }

        return this.Json(new
        {
            enabled = true,
            accountName = config.HolderOf(accounts[0]),
            banks = accounts.Select(a => new
            {
                code = VietnameseBanks.CodeOf(a.Bin),
                name = VietnameseBanks.NameOf(a.Bin),
                bin = a.Bin,
                account = a.Number.Trim(),
                holder = config.HolderOf(a),
            }).ToList(),
            vndPerCoin = config.VndPerCoin,
            minimumVnd = config.MinimumVnd,
            maximumVnd = BankTopUpService.MaximumVnd,
            coinType = config.CoinType.ToString(),
            prefix = config.TransferPrefix.Trim(),
            codes = true,
        });
    }

    /// <summary>
    /// A player asks for a top-up: { account, amount, bin, number, ticket }. Answers the transfer code to write in the
    /// message; the coins are added when Casso/SePay report the transfer or the host confirms it on the admin panel.
    /// The ticket (16-100 characters the browser made up) lets the same browser follow it; only its hash is kept.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns>The top-up, or why not.</returns>
    [HttpPost("pending")]
    public async Task<IActionResult> PendingAsync([FromBody] JsonElement body)
    {
        this.Response.Headers.CacheControl = "no-store";
        var ticket = body.ValueKind == JsonValueKind.Object ? Text(body, "ticket") : string.Empty;
        if (ticket.Length is < 16 or > 100 || !ticket.All(char.IsAsciiLetterOrDigit))
        {
            return this.BadRequest(new { error = "ticket" });
        }

        var config = await this.ReadAsync().ConfigureAwait(false);
        var (result, topUp) = await this._topUps.CreatePendingAsync(Text(body, "account"), Number(body, "amount"), Text(body, "bin"), Text(body, "number"), config, ticket, this.HttpContext.RequestAborted).ConfigureAwait(false);
        if (topUp is null || config is null)
        {
            var error = result.ToString();
            return result == BankTopUpCreateResult.TooMany ? this.StatusCode(StatusCodes.Status429TooManyRequests, new { error }) : this.BadRequest(new { error });
        }

        return this.Json(Describe(topUp, config));
    }

    /// <summary>The top-ups a browser follows: ?t=ticket1,ticket2 (at most 10).</summary>
    /// <param name="t">The tickets, comma separated.</param>
    /// <returns>The top-ups, newest first.</returns>
    [HttpGet("status")]
    public async Task<IActionResult> StatusAsync([FromQuery] string? t)
    {
        this.Response.Headers.CacheControl = "no-store";
        var tickets = (t ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var config = await this.ReadAsync().ConfigureAwait(false) ?? new BankTopUpConfiguration();
        return this.Json(new { topUps = this._topUps.Pending.ForTickets(tickets).Select(p => Describe(p, config)).ToList() });
    }

    /// <summary>
    /// Casso's webhook: header "secure-token", body { error, data: [ { id, tid, description, amount, … } ] }.
    /// Answers 200 with { success: true } once handled, so Casso doesn't send it again.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns>The answer.</returns>
    [HttpPost("casso")]
    public async Task<IActionResult> CassoAsync([FromBody] JsonElement body)
    {
        var config = await this.ReadAsync().ConfigureAwait(false);
        if (config is null || !SameSecret(this.Request.Headers["secure-token"].ToString(), config.CassoSecureToken))
        {
            return this.Unauthorized(new { success = false });
        }

        if (body.ValueKind != JsonValueKind.Object || !body.TryGetProperty("data", out var data))
        {
            return this.BadRequest(new { success = false });
        }

        // An array of transactions (one element per new transaction); an object is taken as one.
        var transactions = data.ValueKind == JsonValueKind.Array ? data.EnumerateArray().ToList() : [data];
        foreach (var transaction in transactions)
        {
            var amount = Number(transaction, "amount");
            if (amount <= 0)
            {
                continue; // money going out
            }

            var transfer = new BankTransfer("casso", Text(transaction, "id"), Text(transaction, "tid") is { Length: > 0 } tid ? tid : Text(transaction, "reference"), amount, Text(transaction, "description"));
            if (await this.TryHandleHostEscrowAsync(transfer, this.HttpContext.RequestAborted).ConfigureAwait(false))
            {
                continue;
            }

            await this._topUps.CreditAsync(transfer, config, this.HttpContext.RequestAborted).ConfigureAwait(false);
        }

        return this.Ok(new { success = true });
    }

    /// <summary>
    /// SePay's webhook: header "Authorization: Apikey KEY", body { id, transferType, transferAmount, content, referenceCode, … }.
    /// Answers 201 with { success: true } once handled (SePay counts only that as delivered).
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns>The answer.</returns>
    [HttpPost("sepay")]
    public async Task<IActionResult> SepayAsync([FromBody] JsonElement body)
    {
        var config = await this.ReadAsync().ConfigureAwait(false);
        var authorization = this.Request.Headers.Authorization.ToString();
        const string scheme = "Apikey ";
        var key = authorization.StartsWith(scheme, StringComparison.OrdinalIgnoreCase) ? authorization[scheme.Length..].Trim() : string.Empty;
        if (config is null || !SameSecret(key, config.SepayApiKey))
        {
            return this.Unauthorized(new { success = false });
        }

        if (body.ValueKind != JsonValueKind.Object)
        {
            return this.BadRequest(new { success = false });
        }

        if (string.Equals(Text(body, "transferType"), "in", StringComparison.OrdinalIgnoreCase))
        {
            var transfer = new BankTransfer("sepay", Text(body, "id"), Text(body, "referenceCode"), Number(body, "transferAmount"), Text(body, "content"));
            if (!await this.TryHandleHostEscrowAsync(transfer, this.HttpContext.RequestAborted).ConfigureAwait(false))
            {
                await this._topUps.CreditAsync(transfer, config, this.HttpContext.RequestAborted).ConfigureAwait(false);
            }
        }

        return this.StatusCode(StatusCodes.Status201Created, new { success = true });
    }

    private static object Describe(BankTopUpPending p, BankTopUpConfiguration config)
    {
        var now = DateTime.UtcNow;
        var account = config.UsableAccounts().FirstOrDefault(a => a.Bin == p.Bin && a.Number.Trim() == p.Number) ?? new BankTopUpAccount { Bin = p.Bin, Number = p.Number };
        return new
        {
            code = p.Code,
            account = p.Login,
            amount = p.AmountVnd,
            coins = p.Coins,
            bin = p.Bin,
            bank = VietnameseBanks.NameOf(p.Bin),
            number = p.Number,
            holder = config.HolderOf(account),
            status = p.Status == BankTopUpPendingStatus.Pending && !p.IsOpen(now) ? "expired" : p.Status.ToString().ToLowerInvariant(),
            created = p.CreatedUtc,
            expires = p.ExpiresUtc,
            done = p.DoneUtc,
        };
    }

    /// <summary>
    /// Compares a sent key with the configured one in constant time; an empty configured key accepts nothing.
    /// </summary>
    private static bool SameSecret(string sent, string configured)
    {
        if (string.IsNullOrEmpty(configured) || string.IsNullOrEmpty(sent))
        {
            return false;
        }

        var a = SHA256.HashData(Encoding.UTF8.GetBytes(sent.Trim()));
        var b = SHA256.HashData(Encoding.UTF8.GetBytes(configured.Trim()));
        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static string Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) ? value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            _ => string.Empty,
        } : string.Empty;

    private static long Number(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            return (long)Math.Floor(number);
        }

        return value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? (long)Math.Floor(parsed)
            : 0;
    }

    private Task<BankTopUpConfiguration?> ReadAsync() =>
        PlugInSettings.ReadActiveAsync<BankTopUpConfiguration>(this._contextProvider, typeof(BankTopUpPlugIn), this.HttpContext.RequestAborted);

    private async Task<bool> TryHandleHostEscrowAsync(BankTransfer transfer, CancellationToken cancellationToken)
    {
        var memo = $"{transfer.Message} {transfer.BankReference}".Trim();
        if (string.IsNullOrEmpty(EscrowHostVndEngine.ExtractHnmoReference(memo)))
        {
            return false;
        }

        var (_, _, savedCfg) = await PlugInSettings.ReadStoredAsync<EscrowConfiguration>(
            this._contextProvider,
            typeof(EscrowContractsPlugIn),
            cancellationToken).ConfigureAwait(false);
        var cfg = savedCfg ?? new EscrowConfiguration();
        if (!EscrowHostVndEngine.IsCompliantAndEnabled(cfg))
        {
            return false;
        }

        var engine = new EscrowHostVndEngine(cfg, EscrowStore.Open(cfg.FileName));
        var (err, matched) = engine.ConfirmHostPaymentReceived(memo, transfer.AmountVnd, $"{transfer.Provider}:{transfer.ProviderId}", DateTime.UtcNow);
        if (err is "Ok" or "AlreadyProcessed")
        {
            if (err == "Ok" && matched is not null)
            {
                ActivityLogPlugIn.RecordExternal(new ActivityLogEntry
                {
                    Id = $"esc-hvp-{Guid.NewGuid():N}",
                    TimestampUtc = DateTime.UtcNow,
                    Kind = ActivityKind.Trade,
                    Account = matched.AcceptedByLogin,
                    PartnerAccount = matched.SellerLogin,
                    Details = $"Host VND payment via {transfer.Provider} released {matched.ContractId} ({transfer.AmountVnd:N0} VND); seller payout queued.",
                });
            }

            return true;
        }

        return false;
    }
}
