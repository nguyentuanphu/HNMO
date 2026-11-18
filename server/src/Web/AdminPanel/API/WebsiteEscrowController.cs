// <copyright file="WebsiteEscrowController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.GameLogic.PlugIns.Escrow;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Escrow Contracts website and account-page endpoints:
/// public browse (<c>GET /api/website/escrow</c>), account contracts &amp; reputation
/// (<c>POST /api/website/escrow/account</c>), and offline-safe seller cancel
/// (<c>POST /api/website/escrow/cancel</c>).
/// </summary>
[ApiController]
[Route("api/website/escrow")]
public sealed class WebsiteEscrowController(
    IPersistenceContextProvider persistence,
    IDataSource<GameConfiguration> gameConfigurationSource,
    IServerProvider serverProvider) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>Gets public open escrow contracts, rules, and aggregate fee income.</summary>
    /// <returns>JSON payload.</returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAsync()
    {
        this.Response.Headers.CacheControl = "no-store";
        var ct = this.HttpContext.RequestAborted;
        var (cfg, engine) = await this.LoadAsync(ct).ConfigureAwait(false);
        if (!cfg.Enabled)
        {
            return this.Content(JsonSerializer.Serialize(new { v = 1, enabled = false }, JsonOptions), "application/json");
        }

        var now = DateTime.UtcNow;
        engine.ExpireDueContracts(now);
        var snapshot = engine.Store.Read(data =>
        {
            var open = data.Contracts
                .Where(c => c.Status == EscrowContractStatus.Open && string.IsNullOrWhiteSpace(c.TargetBuyer))
                .OrderByDescending(c => c.CreatedAtUtc)
                .Take(100)
                .Select(c => ToPublicContractDto(EscrowDirectVndEngine.ProjectForViewer(c, string.Empty)))
                .ToList();
            var feeTotals = data.FeeLedger
                .GroupBy(f => f.Currency, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.FeeAmount), StringComparer.OrdinalIgnoreCase);
            return new
            {
                v = 1,
                enabled = true,
                rules = ToRulesDto(cfg),
                openContracts = open,
                feeTotals,
            };
        });

        return this.Content(JsonSerializer.Serialize(snapshot, JsonOptions), "application/json");
    }

    /// <summary>Gets an authenticated account's escrow contracts, reputation, and owed deliveries.</summary>
    /// <param name="request">Credentials.</param>
    /// <returns>Account escrow view.</returns>
    [HttpPost("account")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.ApiSchemes, Policy = AdminPolicies.AuthenticatedOperator)]
    public async Task<IActionResult> AccountAsync([FromBody] EscrowAccountRequest request)
    {
        var ct = this.HttpContext.RequestAborted;
        var (cfg, engine) = await this.LoadAsync(ct).ConfigureAwait(false);
        if (!cfg.Enabled)
        {
            return this.StatusCode(403, new { error = "off" });
        }

        var configuration = await gameConfigurationSource.GetOwnerAsync(Guid.Empty, ct).ConfigureAwait(false);
        using var context = persistence.CreateNewPlayerContext(configuration);
        if (await AuthenticateAsync(context, request.LoginName, request.Password, ct).ConfigureAwait(false) is not { } account)
        {
            return this.Unauthorized(new { error = "login" });
        }

        var now = DateTime.UtcNow;
        engine.ExpireDueContracts(now);
        var login = account.LoginName;
        var payload = engine.Store.Read(data =>
        {
            var mine = data.Contracts
                .Where(c => string.Equals(c.SellerLogin, login, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(c.TargetBuyer, login, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(c.AcceptedByLogin, login, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(c => c.CreatedAtUtc)
                .Take(60)
                .Select(c => ToAccountContractDto(EscrowDirectVndEngine.ProjectForViewer(c, login), login))
                .ToList();
            var owed = data.OwedDeliveries
                .Where(o => string.Equals(o.RecipientLogin, login, StringComparison.OrdinalIgnoreCase))
                .Select(o => new
                {
                    o.DeliveryId,
                    o.ContractId,
                    o.ReasonEn,
                    o.ReasonVi,
                    o.Zen,
                    itemCount = o.Items.Count,
                    jewelCount = o.Jewels.Sum(j => j.Quantity),
                })
                .ToList();
            var rep = data.Reputations.TryGetValue(login, out var r)
                ? r
                : new EscrowReputationEntry { LoginName = login };
            return new
            {
                v = 1,
                enabled = true,
                accountLogin = login,
                rules = ToRulesDto(cfg),
                reputation = rep,
                contracts = mine,
                owedDeliveries = owed,
            };
        });

        return this.Content(JsonSerializer.Serialize(payload, JsonOptions), "application/json");
    }

    /// <summary>Cancels an open escrow contract owned by the authenticated account (offline-safe via owed deliveries).</summary>
    /// <param name="request">Credentials and contract identifier.</param>
    /// <returns>Updated contract state.</returns>
    [HttpPost("cancel")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.ApiSchemes, Policy = AdminPolicies.AuthenticatedOperator)]
    public async Task<IActionResult> CancelAsync([FromBody] EscrowCancelRequest request)
    {
        var ct = this.HttpContext.RequestAborted;
        var (cfg, engine) = await this.LoadAsync(ct).ConfigureAwait(false);
        if (!cfg.Enabled)
        {
            return this.StatusCode(403, new { error = "off" });
        }

        var configuration = await gameConfigurationSource.GetOwnerAsync(Guid.Empty, ct).ConfigureAwait(false);
        using var context = persistence.CreateNewPlayerContext(configuration);
        if (await AuthenticateAsync(context, request.LoginName, request.Password, ct).ConfigureAwait(false) is not { } account)
        {
            return this.Unauthorized(new { error = "login" });
        }

        var (err, contract) = engine.CancelContract(account.LoginName, request.ContractId?.Trim() ?? string.Empty, isAdmin: false, "Website cancel", DateTime.UtcNow);
        if (err != "Ok" || contract is null)
        {
            return this.BadRequest(new
            {
                error = err,
                messageEn = $"Cannot cancel contract ({err}).",
                messageVi = $"Không thể hủy hợp đồng ({err}).",
            });
        }

        ActivityLogPlugIn.RecordExternal(new ActivityLogEntry
        {
            Id = $"esc-webcancel-{Guid.NewGuid():N}",
            TimestampUtc = DateTime.UtcNow,
            Kind = ActivityKind.Trade,
            Account = account.LoginName,
            ItemSummary = $"Cancelled escrow {contract.ContractId} on website",
            Details = "Seller cancelled contract from website account section; goods queued to owed deliveries.",
        });

        return this.Content(
            JsonSerializer.Serialize(
                new
                {
                    v = 1,
                    result = "Ok",
                    contractId = contract.ContractId,
                    messageEn = "Contract cancelled. Claim returned goods in-game via Contracts (F9).",
                    messageVi = "Đã hủy hợp đồng. Vào game mở Hợp đồng (F9) để nhận lại hàng.",
                },
                JsonOptions),
            "application/json");
    }

    /// <summary>
    /// Pluggable automatic per-seller signed-webhook receiver for Direct P2P VND confirmation:
    /// checks HMAC-SHA256 signature, timestamp window, replay nonce, exact VND amount, and HNMO reference.
    /// </summary>
    /// <param name="contractId">Target Direct P2P VND contract identifier.</param>
    /// <returns>Verification outcome JSON.</returns>
    [HttpPost("direct/webhook/{contractId}")]
    [AllowAnonymous]
    public async Task<IActionResult> DirectWebhookAsync(string contractId)
    {
        var ct = this.HttpContext.RequestAborted;
        var (cfg, engine) = await this.LoadAsync(ct).ConfigureAwait(false);
        var direct = new EscrowDirectVndEngine(cfg, engine.Store);
        if (!EscrowDirectVndEngine.IsCompliantAndEnabled(cfg))
        {
            return this.StatusCode(403, new { error = "DirectVndDisabledOrUnacknowledged" });
        }

        string rawBody;
        using (var reader = new StreamReader(this.Request.Body, Encoding.UTF8))
        {
            rawBody = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
        }

        var signature = this.Request.Headers["X-HNMO-Signature"].FirstOrDefault() ?? string.Empty;
        var tsRaw = this.Request.Headers["X-HNMO-Timestamp"].FirstOrDefault() ?? string.Empty;
        var nonce = this.Request.Headers["X-HNMO-Nonce"].FirstOrDefault() ?? string.Empty;
        if (!long.TryParse(tsRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timestampUnix))
        {
            return this.Unauthorized(new { error = "InvalidTimestamp" });
        }

        long amount = 0;
        var reference = string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            if (doc.RootElement.TryGetProperty("amount", out var amtEl) && amtEl.TryGetInt64(out var parsedAmt))
            {
                amount = parsedAmt;
            }

            if (doc.RootElement.TryGetProperty("reference", out var refEl) && refEl.ValueKind == JsonValueKind.String)
            {
                reference = refEl.GetString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
            return this.BadRequest(new { error = "InvalidJson" });
        }

        var (err, completed) = direct.VerifyAndCompleteWebhook(
            contractId,
            rawBody,
            signature,
            timestampUnix,
            nonce,
            amount,
            reference,
            DateTime.UtcNow);

        if (err is "MissingSignature" or "InvalidSignature" or "StaleTimestamp")
        {
            return this.Unauthorized(new { error = err });
        }

        if (err == "ReplayDetected")
        {
            return this.Conflict(new { error = err });
        }

        if (err != "Ok" || completed is null)
        {
            return this.BadRequest(new { error = err });
        }

        ActivityLogPlugIn.RecordExternal(new ActivityLogEntry
        {
            Id = $"esc-wh-{Guid.NewGuid():N}",
            TimestampUtc = DateTime.UtcNow,
            Kind = ActivityKind.Trade,
            Account = completed.SellerLogin,
            PartnerAccount = completed.AcceptedByLogin,
            Details = $"Direct VND webhook released {completed.ContractId} (amount={amount}, ref={reference})",
        });

        return this.Ok(new { v = 1, result = "Ok", contractId = completed.ContractId, status = completed.Status.ToString() });
    }

    /// <summary>Authenticated buyer receipt upload / "I've paid" endpoint for Direct P2P VND contracts.</summary>
    /// <param name="request">Credentials, contract ID, and optional PNG/JPG/WEBP receipt image.</param>
    /// <returns>Updated status JSON.</returns>
    [HttpPost("direct/receipt")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.ApiSchemes, Policy = AdminPolicies.AuthenticatedOperator)]
    public async Task<IActionResult> DirectReceiptAsync([FromBody] EscrowReceiptRequest request)
    {
        var ct = this.HttpContext.RequestAborted;
        var (cfg, engine) = await this.LoadAsync(ct).ConfigureAwait(false);
        if (!EscrowDirectVndEngine.IsCompliantAndEnabled(cfg))
        {
            return this.StatusCode(403, new { error = "DirectVndDisabledOrUnacknowledged" });
        }

        var configuration = await gameConfigurationSource.GetOwnerAsync(Guid.Empty, ct).ConfigureAwait(false);
        using var context = persistence.CreateNewPlayerContext(configuration);
        if (await AuthenticateAsync(context, request.LoginName, request.Password, ct).ConfigureAwait(false) is not { } account)
        {
            return this.Unauthorized(new { error = "login" });
        }

        var direct = new EscrowDirectVndEngine(cfg, engine.Store);
        var (err, updated) = direct.MarkBuyerPaid(
            account.LoginName,
            request.ContractId?.Trim() ?? string.Empty,
            request.ReceiptMime,
            request.ReceiptBase64,
            request.Note,
            DateTime.UtcNow);
        if (err != "Ok" || updated is null)
        {
            return this.BadRequest(new { error = err });
        }

        return this.Ok(new { v = 1, result = "Ok", contractId = updated.ContractId, status = updated.Status.ToString() });
    }

    private static object ToRulesDto(EscrowConfiguration cfg) => new
    {
        feePercent = cfg.FeePercent,
        minFee = cfg.MinFee,
        maxFee = cfg.MaxFee,
        feePayer = cfg.FeePayer.ToString(),
        allowedCurrencies = cfg.AllowedCurrencies.Select(c => c.ToString()).ToList(),
        maxOpenContracts = cfg.MaxOpenContractsPerAccount,
        cooldownHours = cfg.PostEscrowTradeCooldownHours,
        minAccountAgeDays = cfg.MinAccountAgeDays,
        directVndEnabled = EscrowDirectVndEngine.IsCompliantAndEnabled(cfg),
        directVndPaymentWindowMinutes = cfg.DirectVndPaymentWindowMinutes,
        directVndServiceFeeCurrency = cfg.DirectVndServiceFeeCurrency.ToString(),
        directVndServiceFeeCoins = cfg.DirectVndServiceFeeCoins,
    };

    private static object ToPublicContractDto(EscrowContract c) => new
    {
        c.ContractId,
        c.Mode,
        c.SellerCharacter,
        status = c.Status.ToString(),
        priceCurrency = c.PriceCurrency.ToString(),
        c.PriceAmount,
        c.FeeAmount,
        feePayer = c.FeePayer.ToString(),
        c.BuyerTotalDebit,
        c.EscrowedZen,
        c.EscrowedJewels,
        c.Items,
        c.CreatedAtSydney,
        c.ExpiresAtSydney,
        bankName = c.DirectVnd?.BankName ?? string.Empty,
        sellerBondCoins = c.DirectVnd?.SellerBondCoins ?? 0,
    };

    private static object ToAccountContractDto(EscrowContract c, string login) => new
    {
        c.ContractId,
        c.Mode,
        c.SellerLogin,
        c.SellerCharacter,
        c.TargetBuyer,
        buyerLogin = c.AcceptedByLogin,
        buyerCharacter = c.AcceptedByCharacter,
        isSeller = string.Equals(c.SellerLogin, login, StringComparison.OrdinalIgnoreCase),
        status = c.Status.ToString(),
        c.FrozenByAdmin,
        c.AdminReason,
        priceCurrency = c.PriceCurrency.ToString(),
        c.PriceAmount,
        c.FeeAmount,
        feePayer = c.FeePayer.ToString(),
        c.BuyerTotalDebit,
        c.NetSellerProceeds,
        c.EscrowedZen,
        c.EscrowedJewels,
        c.Items,
        c.CreatedAtSydney,
        c.ExpiresAtSydney,
        c.DirectVnd,
    };

    private static async Task<Account?> AuthenticateAsync(IPlayerContext context, string? loginName, string? password, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(loginName) || string.IsNullOrEmpty(password) || loginName.Length > 10 || password.Length > 20)
        {
            return null;
        }

        var account = await context.GetAccountByLoginNameAsync(loginName.Trim(), password, cancellationToken).ConfigureAwait(false);
        return account is null || account.IsBot || account.State is AccountState.Banned or AccountState.TemporarilyBanned ? null : account;
    }

    private async Task<(EscrowConfiguration Config, EscrowEngine Engine)> LoadAsync(CancellationToken cancellationToken)
    {
        foreach (var ctx in serverProvider.Servers.OfType<IGameServerContextProvider>().Select(s => s.Context).Distinct())
        {
            if (EscrowContractsPlugIn.Get(ctx) is { } plugin)
            {
                return (plugin.Configuration ?? new EscrowConfiguration(), plugin.GetEngine());
            }
        }

        var (_, _, saved) = await PlugInSettings.ReadStoredAsync<EscrowConfiguration>(persistence, typeof(EscrowContractsPlugIn), cancellationToken).ConfigureAwait(false);
        var cfg = saved ?? new EscrowConfiguration();
        return (cfg, new EscrowEngine(cfg, EscrowStore.Open(cfg.FileName)));
    }

    /// <summary>Account query request payload.</summary>
    /// <param name="LoginName">Account login name.</param>
    /// <param name="Password">Account password.</param>
    public sealed record EscrowAccountRequest(string LoginName, string Password);

    /// <summary>Contract cancel request payload.</summary>
    /// <param name="LoginName">Account login name.</param>
    /// <param name="Password">Account password.</param>
    /// <param name="ContractId">Target contract identifier.</param>
    public sealed record EscrowCancelRequest(string LoginName, string Password, string ContractId);

    /// <summary>Buyer receipt / payment confirmation payload.</summary>
    /// <param name="LoginName">Buyer login name.</param>
    /// <param name="Password">Buyer password.</param>
    /// <param name="ContractId">Target contract identifier.</param>
    /// <param name="ReceiptMime">Optional image MIME type.</param>
    /// <param name="ReceiptBase64">Optional base64 receipt image.</param>
    /// <param name="Note">Optional payment reference note.</param>
    public sealed record EscrowReceiptRequest(
        string LoginName,
        string Password,
        string ContractId,
        string? ReceiptMime,
        string? ReceiptBase64,
        string? Note);
}
