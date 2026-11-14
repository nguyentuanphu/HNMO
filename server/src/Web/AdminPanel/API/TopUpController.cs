// <copyright file="TopUpController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using HNMO.GameLogic.PlugIns;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.Services;

/// <summary>
/// Top-ups by bank transfer (the "Bank top-up" plug-in): what the web game's top-up page shows, and the
/// webhooks of Casso and SePay, which report incoming transfers. No login: the page is public, and the
/// webhooks prove themselves with the keys set in the plug-in, compared in constant time.
/// </summary>
[Route("api/topup/")]
[AllowAnonymous]
public class TopUpController : Controller
{
    /// <summary>The VietQR (NAPAS) bank ids.</summary>
    private static readonly (string Code, string Name, string Bin)[] Banks =
    [
        ("TCB", "Techcombank", "970407"),
        ("VCB", "Vietcombank", "970436"),
    ];

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
        var accounts = config is null ? [] : Banks
            .Select(bank => (bank, account: AccountOf(config, bank.Code)))
            .Where(entry => entry.account.Length > 0)
            .Select(entry => new { code = entry.bank.Code, name = entry.bank.Name, bin = entry.bank.Bin, account = entry.account })
            .ToList();
        if (config is null || accounts.Count == 0)
        {
            return this.Json(new { enabled = false });
        }

        return this.Json(new
        {
            enabled = true,
            accountName = config.AccountName.Trim(),
            banks = accounts,
            vndPerCoin = config.VndPerCoin,
            minimumVnd = config.MinimumVnd,
            coinType = config.CoinType.ToString(),
            prefix = config.TransferPrefix.Trim(),
        });
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
            await this._topUps.CreditAsync(transfer, config, this.HttpContext.RequestAborted).ConfigureAwait(false);
        }

        return this.StatusCode(StatusCodes.Status201Created, new { success = true });
    }

    private static string AccountOf(BankTopUpConfiguration config, string bankCode) => (bankCode switch
    {
        "TCB" => config.TechcombankAccount,
        "VCB" => config.VietcombankAccount,
        _ => string.Empty,
    }).Trim();

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
}
