// <copyright file="WebsiteTopUpMilestonesController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.GameLogic.PlugIns.TopUpMilestones;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.Auth;
using HNMO.Web.AdminPanel.Services;

/// <summary>
/// Public and authenticated website endpoints for <see cref="TopUpMilestonesPlugIn"/>.
/// </summary>
[ApiController]
[Route("api/website/topup-milestones")]
public sealed class WebsiteTopUpMilestonesController(
    IPersistenceContextProvider persistence,
    IDataSource<GameConfiguration> gameConfigurationSource,
    ILogger<WebsiteTopUpMilestonesController> logger) : ControllerBase
{
    /// <summary>Gets the public milestone tiers (no account details).</summary>
    /// <returns>The public JSON payload.</returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAsync()
    {
        this.Response.Headers.CacheControl = "no-store";
        var cancellationToken = this.HttpContext.RequestAborted;
        if (await this.EngineAsync(cancellationToken).ConfigureAwait(false) is not { } engine)
        {
            return this.Content("{\"v\":1,\"enabled\":false}", "application/json");
        }

        return this.Content(TopUpMilestoneStateWriter.WritePublic(engine, await this.ItemNamesAsync(cancellationToken).ConfigureAwait(false)), "application/json");
    }

    /// <summary>Gets an account's cumulative top-up VND, step progress, and recent history.</summary>
    /// <param name="request">The login name and password.</param>
    /// <returns>The account progress JSON payload.</returns>
    [HttpPost("account")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.ApiSchemes, Policy = AdminPolicies.AuthenticatedOperator)]
    public async Task<IActionResult> AccountAsync([FromBody] TopUpMilestoneAccountRequest request)
    {
        var cancellationToken = this.HttpContext.RequestAborted;
        if (await this.EngineAsync(cancellationToken).ConfigureAwait(false) is not { Configuration.AllowWebsite: true } engine)
        {
            return this.StatusCode(403, new { error = "off" });
        }

        var configuration = await gameConfigurationSource.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
        if (await this.AccountAsync(configuration, request.LoginName, request.Password, cancellationToken).ConfigureAwait(false) is not { } account)
        {
            return this.Unauthorized(new { error = "login" });
        }

        return this.Content(TopUpMilestoneStateWriter.WriteAccountState(engine, account.LoginName, TopUpMilestonesPlugIn.ItemNames(configuration)), "application/json");
    }

    /// <summary>Claims a reached milestone step from the website account page.</summary>
    /// <param name="request">The login name, password, and milestone step id.</param>
    /// <returns>The updated account progress JSON payload.</returns>
    [HttpPost("claim")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.ApiSchemes, Policy = AdminPolicies.AuthenticatedOperator)]
    public async Task<IActionResult> ClaimAsync([FromBody] TopUpMilestoneClaimRequest request)
    {
        var cancellationToken = this.HttpContext.RequestAborted;
        if (await this.EngineAsync(cancellationToken).ConfigureAwait(false) is not { Configuration.AllowWebsite: true } engine)
        {
            return this.StatusCode(403, new { error = "off" });
        }

        var configuration = await gameConfigurationSource.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
        if (await this.AccountAsync(configuration, request.LoginName, request.Password, cancellationToken).ConfigureAwait(false) is not { } account)
        {
            return this.Unauthorized(new { error = "login" });
        }

        var outcome = engine.Claim(account.LoginName, request.StepId ?? string.Empty, DateTime.UtcNow);
        if (outcome.Result == TopUpClaimResult.Ok && outcome.CoinGrant is { } grant)
        {
            await this.GrantCoinsAsync(configuration, grant, cancellationToken).ConfigureAwait(false);
        }

        lock (engine.Store.Lock)
        {
            engine.Store.SaveIfChanged();
        }

        var (en, vi) = outcome.Result switch
        {
            TopUpClaimResult.Ok => ($"Milestone claimed: {outcome.Step?.TitleEn}!", $"Đã nhận mốc nạp: {outcome.Step?.TitleVi}!"),
            TopUpClaimResult.AlreadyClaimed => ("You already claimed this milestone.", "Bạn đã nhận mốc nạp này rồi."),
            TopUpClaimResult.NotReached => ("Your total top-up has not reached this milestone yet.", "Tổng nạp của bạn chưa đạt mốc này."),
            _ => ("Milestone step not found.", "Không tìm thấy mốc nạp."),
        };

        return this.Content(
            TopUpMilestoneStateWriter.WriteAccountState(engine, account.LoginName, TopUpMilestonesPlugIn.ItemNames(configuration), outcome.Result.ToString(), en, vi),
            "application/json");
    }

    private async Task GrantCoinsAsync(GameConfiguration configuration, CoinGrant grant, CancellationToken cancellationToken)
    {
        foreach (var context in TopUpMilestonesPlugIn.KnownContexts())
        {
            if (TopUpMilestonesPlugIn.Get(context) is not null)
            {
                await MissionCoinGranter.GrantAsync(context, grant, TopUpMilestoneEngine.GrantedBy).ConfigureAwait(false);
                return;
            }
        }

        var coinService = new CashShopCoinService(persistence, gameConfigurationSource);
        await coinService.GrantAsync(grant.LoginName, grant.CoinType, grant.Amount, grant.Reason, grant.Reference, TopUpMilestoneEngine.GrantedBy, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Account?> AccountAsync(GameConfiguration configuration, string? loginName, string? password, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(loginName) || string.IsNullOrEmpty(password) || loginName.Length > 10 || password.Length > 20)
        {
            return null;
        }

        using var context = persistence.CreateNewPlayerContext(configuration);
        var account = await context.GetAccountByLoginNameAsync(loginName.Trim(), password, cancellationToken).ConfigureAwait(false);
        return account is null || account.IsBot || account.State is AccountState.Banned or AccountState.TemporarilyBanned ? null : account;
    }

    private async Task<TopUpMilestoneEngine?> EngineAsync(CancellationToken cancellationToken)
    {
        var configuration = await PlugInSettings.ReadActiveAsync<TopUpMilestonesConfiguration>(persistence, typeof(TopUpMilestonesPlugIn), () => new TopUpMilestonesConfiguration(), cancellationToken).ConfigureAwait(false);
        return configuration is null ? null : new TopUpMilestoneEngine(configuration, TopUpMilestoneStore.Open(configuration.FileName), enabled: true);
    }

    private async Task<Func<short, short, string?>> ItemNamesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return TopUpMilestonesPlugIn.ItemNames(await gameConfigurationSource.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not load item names for top-up milestones.");
            return static (_, _) => null;
        }
    }
}

/// <summary>Request to inspect an account's top-up milestone progress.</summary>
/// <param name="LoginName">The account login name.</param>
/// <param name="Password">The account password.</param>
public sealed record TopUpMilestoneAccountRequest(string? LoginName, string? Password);

/// <summary>Request to claim a reached milestone step on the website.</summary>
/// <param name="LoginName">The account login name.</param>
/// <param name="Password">The account password.</param>
/// <param name="StepId">The step id.</param>
public sealed record TopUpMilestoneClaimRequest(string? LoginName, string? Password, string? StepId);
