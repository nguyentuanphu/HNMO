// <copyright file="WebsiteLuckyNumberController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns.LuckyNumber;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// Website endpoints for <see cref="LuckyNumberPlugIn"/>: public draw board and authenticated web picks.
/// </summary>
[ApiController]
[Route("api/website/lucky-number")]
public sealed class WebsiteLuckyNumberController(
    IPersistenceContextProvider persistence,
    IDataSource<GameConfiguration> gameConfigurationSource,
    ILogger<WebsiteLuckyNumberController> logger) : ControllerBase
{
    /// <summary>Gets public Daily Lucky Number rules, odds, prizes, and recent draws.</summary>
    /// <returns>Plain-ASCII JSON payload.</returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAsync()
    {
        this.Response.Headers.CacheControl = "no-store";
        var ct = this.HttpContext.RequestAborted;
        var engine = await this.EngineAsync(ct).ConfigureAwait(false);
        return this.Content(
            LuckyNumberStateWriter.WritePublic(engine, DateTime.UtcNow, await this.ItemNamesAsync(ct).ConfigureAwait(false)),
            "application/json");
    }

    /// <summary>Gets an authenticated account's current picks, owed jewels, and draw status.</summary>
    /// <param name="request">The login credentials.</param>
    /// <returns>Account lucky number JSON.</returns>
    [HttpPost("account")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.ApiSchemes, Policy = AdminPolicies.AuthenticatedOperator)]
    public async Task<IActionResult> AccountAsync([FromBody] LuckyNumberAccountRequest request)
    {
        var ct = this.HttpContext.RequestAborted;
        var engine = await this.EngineAsync(ct).ConfigureAwait(false);
        if (!engine.Enabled || !engine.Configuration.AllowWebsite)
        {
            return this.StatusCode(403, new { error = "off" });
        }

        var gameConfig = await gameConfigurationSource.GetOwnerAsync(Guid.Empty, ct).ConfigureAwait(false);
        if (await this.AccountAsync(gameConfig, request.LoginName, request.Password, ct).ConfigureAwait(false) is not { } account)
        {
            return this.Unauthorized(new { error = "login" });
        }

        return this.Content(
            LuckyNumberStateWriter.WriteAccountState(engine, account.LoginName, DateTime.UtcNow, LuckyNumberPlugIn.ItemNames(gameConfig)),
            "application/json");
    }

    /// <summary>Submits a lucky number pick from the website for an authenticated account.</summary>
    /// <param name="request">The login credentials and picked number.</param>
    /// <returns>Updated account lucky number JSON.</returns>
    [HttpPost("pick")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.ApiSchemes, Policy = AdminPolicies.AuthenticatedOperator)]
    public async Task<IActionResult> PickAsync([FromBody] LuckyNumberPickRequest request)
    {
        var ct = this.HttpContext.RequestAborted;
        var engine = await this.EngineAsync(ct).ConfigureAwait(false);
        if (!engine.Enabled || !engine.Configuration.AllowWebsite)
        {
            return this.StatusCode(403, new { error = "off" });
        }

        var gameConfig = await gameConfigurationSource.GetOwnerAsync(Guid.Empty, ct).ConfigureAwait(false);
        if (await this.AccountAsync(gameConfig, request.LoginName, request.Password, ct).ConfigureAwait(false) is not { } account)
        {
            return this.Unauthorized(new { error = "login" });
        }

        var outcome = engine.Pick(account.LoginName, account.LoginName, request.Number, DateTime.UtcNow, "website");
        var (en, vi) = outcome switch
        {
            LuckyNumberPickResult.Success => ($"Picked lucky number {request.Number:00}!", $"Đã chọn số may mắn {request.Number:00}!"),
            LuckyNumberPickResult.OutOfRange => ($"Number must be between {engine.Configuration.MinNumber} and {engine.Configuration.MaxNumber}.", $"Số phải từ {engine.Configuration.MinNumber} đến {engine.Configuration.MaxNumber}."),
            LuckyNumberPickResult.DailyLimitReached => ("You already reached today's pick limit.", "Bạn đã đạt giới hạn chọn số hôm nay."),
            LuckyNumberPickResult.DuplicateNumber => ("You already picked that number for this draw.", "Bạn đã chọn số này cho kỳ quay hôm nay."),
            _ => ("Daily lucky number is currently turned off.", "Tính năng Con số may mắn hiện đang tắt."),
        };

        return this.Content(
            LuckyNumberStateWriter.WriteAccountState(engine, account.LoginName, DateTime.UtcNow, LuckyNumberPlugIn.ItemNames(gameConfig), outcome.ToString(), en, vi),
            "application/json");
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

    private async Task<LuckyNumberEngine> EngineAsync(CancellationToken cancellationToken)
    {
        var (_, isActive, saved) = await PlugInSettings.ReadStoredAsync<LuckyNumberConfiguration>(persistence, typeof(LuckyNumberPlugIn), cancellationToken).ConfigureAwait(false);
        var cfg = saved ?? new LuckyNumberConfiguration();
        return new LuckyNumberEngine(cfg, LuckyNumberStore.Open(cfg.FileName), isActive);
    }

    private async Task<Func<short, short, string?>> ItemNamesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return LuckyNumberPlugIn.ItemNames(await gameConfigurationSource.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not load item names for Daily Lucky Number.");
            return static (_, _) => null;
        }
    }
}
