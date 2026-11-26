// <copyright file="WebsiteJewelBankController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Security.Cryptography;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.JewelBank;
using HNMO.GameLogic.PlugIns.ResetOptions;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// Website Jewel Bank API:
/// public rules (<c>GET /api/website/jewel-bank</c>),
/// authenticated account balances (<c>POST /api/website/jewel-bank/account</c>),
/// withdraw to in-game claim (<c>POST /api/website/jewel-bank/withdraw</c>),
/// and clear master skill points (<c>POST /api/website/jewel-bank/clear-master</c>).
/// </summary>
[ApiController]
[Route("api/website/jewel-bank")]
public sealed class WebsiteJewelBankController(
    IPersistenceContextProvider persistence,
    IDataSource<GameConfiguration> gameConfigurationSource,
    ILoginServer loginServer,
    IServerProvider serverProvider,
    ILogger<WebsiteJewelBankController> logger) : ControllerBase
{
    /// <summary>Gets public Jewel Bank rules.</summary>
    /// <returns>Plain-ASCII JSON.</returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAsync()
    {
        this.Response.Headers.CacheControl = "no-store";
        var cancellationToken = this.HttpContext.RequestAborted;
        if (await this.EngineAsync(cancellationToken).ConfigureAwait(false) is not { Configuration.AllowWebsite: true } engine)
        {
            return this.Content("{\"v\":1,\"enabled\":false}", "application/json");
        }

        return this.Content(JewelBankStateWriter.WritePublic(engine), "application/json");
    }

    /// <summary>Gets an account's Jewel Bank balances, pending deliveries, and character master-point summaries.</summary>
    /// <param name="request">Credentials.</param>
    /// <returns>Account Jewel Bank JSON.</returns>
    [HttpPost("account")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.ApiSchemes, Policy = AdminPolicies.AuthenticatedOperator)]
    public async Task<IActionResult> AccountAsync([FromBody] JewelBankAccountRequest request)
    {
        var cancellationToken = this.HttpContext.RequestAborted;
        if (await this.EngineAsync(cancellationToken).ConfigureAwait(false) is not { Configuration.AllowWebsite: true } engine)
        {
            return this.StatusCode(403, new { error = "off" });
        }

        var configuration = await gameConfigurationSource.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
        using var context = persistence.CreateNewPlayerContext(configuration);
        if (await AuthenticateAsync(context, request.LoginName, request.Password, cancellationToken).ConfigureAwait(false) is not { } account)
        {
            return this.Unauthorized(new { error = "login" });
        }

        return this.Content(await this.SerializeAccountResponseAsync(engine, account).ConfigureAwait(false), "application/json");
    }

    /// <summary>Withdraws banked jewels into the player's pending in-game claim queue (delivered automatically on next login or F7).</summary>
    /// <param name="request">Credentials, jewel key, and quantity.</param>
    /// <returns>Updated account state and result code.</returns>
    [HttpPost("withdraw")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.ApiSchemes, Policy = AdminPolicies.AuthenticatedOperator)]
    public async Task<IActionResult> WithdrawAsync([FromBody] JewelBankWithdrawRequest request)
    {
        var cancellationToken = this.HttpContext.RequestAborted;
        if (await this.EngineAsync(cancellationToken).ConfigureAwait(false) is not { Configuration.AllowWebsite: true } engine)
        {
            return this.StatusCode(403, new { error = "off" });
        }

        var configuration = await gameConfigurationSource.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
        using var context = persistence.CreateNewPlayerContext(configuration);
        if (await AuthenticateAsync(context, request.LoginName, request.Password, cancellationToken).ConfigureAwait(false) is not { } account)
        {
            return this.Unauthorized(new { error = "login" });
        }

        var qty = Math.Clamp(request.Quantity, 1, 50);
        var key = request.JewelKey?.Trim() ?? string.Empty;
        if (engine.FindByKey(key) is not { } jewel)
        {
            return this.Content(
                await this.SerializeAccountResponseAsync(engine, account, nameof(JewelBankResult.JewelNotAllowed), "Insufficient banked jewels or invalid jewel.", "Số dư ngọc không đủ hoặc loại ngọc không hợp lệ.").ConfigureAwait(false),
                "application/json");
        }

        // The bank is debited and the delivery written in one transaction. The caller's operation id makes a retry of
        // the same request (e.g. after a lost answer) find the first one instead of withdrawing again.
        var operationId = request.OperationId is { } given && given != Guid.Empty ? given : Guid.NewGuid();
        var payload = $"web-withdraw|{jewel.Key}:{qty}";
        var operation = new AssetOperationRequest(
            operationId,
            account.GetId(),
            JewelBankPlugIn.WebsiteWithdrawKind,
            JewelBankPlugIn.PayloadHash(payload),
            "website",
            payload,
            [new JewelBankDelta(jewel.Key, -qty, int.MaxValue)],
            jewel.Key,
            qty);
        AssetCommitStatus status;
        try
        {
            status = await context.SaveChangesWithAssetOperationAsync(operation, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The answer of the commit may be lost: what the database holds decides.
            logger.LogWarning(ex, "Jewel Bank (website): the withdrawal {Operation} of {Login} failed or lost its answer.", operationId, account.LoginName);
            using var check = persistence.CreateNewPlayerContext(configuration);
            if (await check.GetAssetOperationAsync(operationId, cancellationToken).ConfigureAwait(false) is not { } receipt)
            {
                return this.StatusCode(503, new { error = "retry", operationId });
            }

            status = receipt.PayloadHash == operation.PayloadHash ? AssetCommitStatus.AlreadyCommitted : AssetCommitStatus.Conflict;
        }

        if (status == AssetCommitStatus.Conflict)
        {
            return this.Conflict(new { error = "operationId", operationId });
        }

        if (status == AssetCommitStatus.Refused)
        {
            return this.Content(
                await this.SerializeAccountResponseAsync(engine, account, nameof(JewelBankResult.InsufficientBalance), "Insufficient banked jewels or invalid jewel.", "Số dư ngọc không đủ hoặc loại ngọc không hợp lệ.").ConfigureAwait(false),
                "application/json");
        }

        logger.LogInformation("Jewel Bank (website): {Login} queued {Qty} x {Key} for in-game delivery.", account.LoginName, qty, key);
        await this.RefreshOnlineAsync(account.LoginName).ConfigureAwait(false);
        return this.Content(
            await this.SerializeAccountResponseAsync(engine, account, "Queued", $"Withdrew {qty} × {key}! Claim automatically on next login or press F7.", $"Đã rút {qty} × {key}! Tự động nhận khi vào game hoặc bấm F7.").ConfigureAwait(false),
            "application/json");
    }

    /// <summary>Clears and refunds master skill points for an offline character.</summary>
    /// <param name="request">Credentials and character name.</param>
    /// <returns>Updated account state and result code.</returns>
    [HttpPost("clear-master")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.ApiSchemes, Policy = AdminPolicies.AuthenticatedOperator)]
    public async Task<IActionResult> ClearMasterAsync([FromBody] JewelBankClearMasterRequest request)
    {
        var cancellationToken = this.HttpContext.RequestAborted;
        if (await this.EngineAsync(cancellationToken).ConfigureAwait(false) is not { Configuration.AllowWebsite: true } engine)
        {
            return this.StatusCode(403, new { error = "off" });
        }

        if (!engine.Configuration.AllowClearMasterSkills)
        {
            return this.StatusCode(403, new { error = "clearDisabled" });
        }

        var configuration = await gameConfigurationSource.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
        using (var check = persistence.CreateNewPlayerContext(configuration))
        {
            if (await AuthenticateAsync(check, request.LoginName, request.Password, cancellationToken).ConfigureAwait(false) is not { } checkedAccount)
            {
                return this.Unauthorized(new { error = "login" });
            }

            if (checkedAccount.Characters.All(c => !string.Equals(c.Name, request.Character, StringComparison.Ordinal)))
            {
                return this.BadRequest(new { error = "character" });
            }
        }

        var login = request.LoginName!.Trim();
        var reservation = RandomNumberGenerator.GetInt32(byte.MaxValue + 1, int.MaxValue);
        if (!await loginServer.TryReserveAccountAsync(login, reservation).ConfigureAwait(false))
        {
            using var readCtx = persistence.CreateNewPlayerContext(configuration);
            var readAccount = await readCtx.GetAccountByLoginNameAsync(login, cancellationToken).ConfigureAwait(false);
            return this.Content(
                await this.SerializeAccountResponseAsync(engine, readAccount!, "Online", "Log out of the game before clearing master skills on the website.", "Hãy thoát game trước khi tẩy điểm master trên website.").ConfigureAwait(false),
                "application/json");
        }

        try
        {
            using var context = persistence.CreateNewPlayerContext(configuration);
            var account = await context.GetAccountByLoginNameAsync(login, cancellationToken).ConfigureAwait(false);
            var character = account?.Characters.FirstOrDefault(c => c.Name == request.Character);
            if (account is null || character is null)
            {
                return this.BadRequest(new { error = "character" });
            }

            var spent = ResetApplier.CountSpentMasterPoints(character);
            if (spent <= 0)
            {
                return this.Content(
                    await this.SerializeAccountResponseAsync(engine, account, "NothingToClear", "Character has no spent master points.", "Nhân vật chưa cộng điểm master nào.").ConfigureAwait(false),
                    "application/json");
            }

            var zen = Math.Max(0, engine.Configuration.ClearMasterZen);
            if ((character.Inventory?.Money ?? 0) < zen)
            {
                return this.Content(
                    await this.SerializeAccountResponseAsync(engine, account, "NotEnoughZen", $"Need {zen:N0} Zen.", $"Cần {zen:N0} Zen.").ConfigureAwait(false),
                    "application/json");
            }

            var plan = new ResetPlan();
            if (engine.Configuration.ClearMasterCoins > 0)
            {
                plan.Coins.Add(new ResetCoinCost(engine.Configuration.ClearMasterCoinType, engine.Configuration.ClearMasterCoins));
            }

            if (!ResetApplier.TryTakeCoins(account, plan))
            {
                return this.Content(
                    await this.SerializeAccountResponseAsync(engine, account, "NotEnoughCoins", "Insufficient coins.", "Không đủ coin.").ConfigureAwait(false),
                    "application/json");
            }

            if (zen > 0)
            {
                character.Inventory!.Money -= zen;
            }

            var refunded = await ResetApplier.ClearMasterSkillsOfflineAsync(character, context).ConfigureAwait(false);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            engine.RecordClearMaster(login, character.Name, refunded, "website", DateTime.UtcNow);
            return this.Content(
                await this.SerializeAccountResponseAsync(engine, account, "ClearedMaster", $"Refunded {refunded} master point(s) on {character.Name}.", $"Đã hoàn {refunded} điểm master cho {character.Name}.").ConfigureAwait(false),
                "application/json");
        }
        finally
        {
            await loginServer.ReleaseAccountReservationAsync(login, reservation).ConfigureAwait(false);
        }
    }

    private static async Task<Account?> AuthenticateAsync(IPlayerContext context, string? loginName, string? password, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(loginName) || string.IsNullOrEmpty(password) || loginName.Length > 10 || password.Length > 20)
        {
            return null;
        }

        var account = await context.GetAccountByLoginNameAsync(loginName.Trim(), password, cancellationToken).ConfigureAwait(false);
        return account is null || account.IsBot || account.State is AccountState.Banned or AccountState.TemporarilyBanned ? null : account;
    }

    private async Task<string> SerializeAccountResponseAsync(
        JewelBankEngine engine,
        Account account,
        string? result = null,
        string? messageEn = null,
        string? messageVi = null)
    {
        var (balances, pending) = await this.ReadBankAsync(account).ConfigureAwait(false);
        var stateJson = JewelBankStateWriter.WriteAccountState(engine, balances, pending, 0, 0, bagJewels: null, result, messageEn, messageVi);
        var busy = false;
        try
        {
            var snap = await loginServer.GetSnapshotAsync().ConfigureAwait(false);
            busy = snap.Keys.Any(k => string.Equals(k, account.LoginName, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            // Ignore snapshot failures.
        }

        var charsJson = System.Text.Json.JsonSerializer.Serialize(account.Characters.OrderBy(c => c.CharacterSlot).Select(c => new
        {
            name = c.Name,
            masterFreePoints = c.MasterLevelUpPoints,
            masterSpentPoints = ResetApplier.CountSpentMasterPoints(c),
            zen = c.Inventory?.Money ?? 0,
        }));

        return $"{{\"v\":1,\"busy\":{(busy ? "true" : "false")},\"state\":{stateJson},\"characters\":{charsJson}}}";
    }

    private async Task RefreshOnlineAsync(string login)
    {
        foreach (var ctx in serverProvider.Servers.OfType<IGameServerContextProvider>().Select(s => s.Context).Distinct())
        {
            var plugIn = JewelBankPlugIn.Get(ctx);
            if (plugIn is null)
            {
                continue;
            }

            var players = await ctx.GetPlayersAsync().ConfigureAwait(false);
            foreach (var p in players.Where(p => string.Equals(p.Account?.LoginName, login, StringComparison.OrdinalIgnoreCase)))
            {
                await plugIn.SendStateAsync(p, requested: false).ConfigureAwait(false);
            }
        }
    }

    private async Task<JewelBankEngine?> EngineAsync(CancellationToken cancellationToken)
    {
        var configuration = await PlugInSettings.ReadActiveAsync<JewelBankConfiguration>(persistence, typeof(JewelBankPlugIn), () => new JewelBankConfiguration(), cancellationToken).ConfigureAwait(false);
        return configuration is null ? null : new JewelBankEngine(configuration, JewelBankStore.Open(configuration.FileName), enabled: true);
    }

    /// <summary>Reads an account's balances and waiting deliveries from the database, with a context of their own.</summary>
    /// <param name="account">The account.</param>
    /// <returns>The balances and the waiting deliveries.</returns>
    private async Task<(IReadOnlyDictionary<string, int> Balances, IReadOnlyList<(string Id, string Key, int Quantity)> Pending)> ReadBankAsync(Account account)
    {
        var configuration = await gameConfigurationSource.GetOwnerAsync(Guid.Empty).ConfigureAwait(false);
        using var context = persistence.CreateNewPlayerContext(configuration);
        var accountId = account.GetId();
        var balances = await context.GetJewelBankBalancesAsync(accountId).ConfigureAwait(false);
        var pending = (await context.GetPendingAssetOperationsAsync(accountId).ConfigureAwait(false))
            .Where(operation => operation.Kind == JewelBankPlugIn.WebsiteWithdrawKind && operation.PendingKey is not null)
            .Select(operation => (operation.Id.ToString("N"), operation.PendingKey!, operation.PendingCount))
            .ToList();
        return (balances, pending);
    }
}
