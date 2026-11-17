// <copyright file="WebsiteVipController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Security.Cryptography;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns.Vip;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>Own-account VIP operations through the authenticated website bridge; mutations require an offline reservation.</summary>
/// <param name="persistence">The account persistence provider.</param>
/// <param name="configurations">The game configuration source.</param>
/// <param name="loginServer">The account-ownership authority.</param>
/// <param name="logger">The operational logger.</param>
/// <param name="state">Optional shared-state override for hosts and isolated tests.</param>
/// <param name="clock">The host clock.</param>
[ApiController]
[Route("api/website/account/vip")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.ApiSchemes, Policy = AdminPolicies.AuthenticatedOperator)]
[RequestSizeLimit(2048)]
public sealed class WebsiteVipController(
    IPersistenceContextProvider persistence,
    IDataSource<GameConfiguration> configurations,
    ILoginServer loginServer,
    ILogger<WebsiteVipController> logger,
    VipStateStore? state = null,
    TimeProvider? clock = null) : ControllerBase
{
    private readonly VipStateStore _state = state ?? VipStateStore.OpenHost();
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>Reads the authenticated account's paid VIP status, offers and recovery selection.</summary>
    /// <param name="request">The account credentials and optional retry identifier.</param>
    /// <returns>Safe account-owned state.</returns>
    [HttpPost("state")]
    public Task<IActionResult> StateAsync([FromBody] WebsiteVipRequest request) => this.HandleAsync(request, "state");

    /// <summary>Buys or retries the reviewed offer after authenticating again under an offline reservation.</summary>
    /// <param name="request">The credentials and reviewed offer.</param>
    /// <returns>The current state and a localized-by-the-client outcome.</returns>
    [HttpPost("purchase")]
    public Task<IActionResult> PurchaseAsync([FromBody] WebsiteVipRequest request) => this.HandleAsync(request, "purchase");

    /// <summary>Records the paid subscription's daily coins using the unique Sydney-day reward reference.</summary>
    /// <param name="request">The account credentials.</param>
    /// <returns>The current state and durable credit outcome.</returns>
    [HttpPost("daily")]
    public Task<IActionResult> DailyAsync([FromBody] WebsiteVipRequest request) => this.HandleAsync(request, "daily");

    private static async ValueTask<Account?> AuthenticateAsync(IPlayerContext context, WebsiteVipRequest request, CancellationToken cancellationToken)
    {
        if (request.LoginName is not { Length: > 0 and <= 10 } login || request.Password is not { Length: > 0 and <= 20 } password)
        {
            return null;
        }

        var account = await context.GetAccountByLoginNameAsync(login, cancellationToken).ConfigureAwait(false);
        if (account is not { IsBot: false } || account.State is AccountState.Banned or AccountState.TemporarilyBanned || string.IsNullOrEmpty(account.PasswordHash))
        {
            return null;
        }

        return BCrypt.Net.BCrypt.Verify(password, account.PasswordHash) ? account : null;
    }

    private async Task<IActionResult> HandleAsync(WebsiteVipRequest request, string action)
    {
        this.Response.Headers.CacheControl = "no-store";
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(this.HttpContext.RequestAborted);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        var cancellationToken = deadline.Token;
        var reservation = RandomNumberGenerator.GetInt32(byte.MaxValue + 1, int.MaxValue);
        var reserved = false;
        string? login = null;
        try
        {
            var game = await configurations.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
            var saved = await PlugInSettings.ReadStoredAsync<VipConfiguration>(persistence, typeof(VipPlugIn), cancellationToken).ConfigureAwait(false);
            var configuration = saved.Settings ?? new VipConfiguration();
            if (!saved.IsActive)
            {
                configuration.Enabled = false;
            }

            var wallet = new VipWallet(configuration, this._state, game, logger);
            using var initial = persistence.CreateNewPlayerContext(game);
            if (await AuthenticateAsync(initial, request, cancellationToken).ConfigureAwait(false) is not { } account)
            {
                return this.Unauthorized(new { error = "login" });
            }

            if (action == "state")
            {
                return await this.ReplyAsync(configuration, wallet, initial, account, request.OperationId).ConfigureAwait(false);
            }

            if (action == "purchase" && (request.OperationId is not { } operation || operation == Guid.Empty || request.OfferToken is not { Length: 64 } token || !token.All(Uri.IsHexDigit)))
            {
                return this.BadRequest(new { error = "vip_request" });
            }

            login = account.LoginName;
            reserved = await loginServer.TryReserveAccountAsync(login, reservation).AsTask().WaitAsync(cancellationToken).ConfigureAwait(false);
            if (!reserved)
            {
                return await this.ReplyAsync(configuration, wallet, initial, account, request.OperationId, "Online").ConfigureAwait(false);
            }

            using var owned = persistence.CreateNewPlayerContext(game);
            if (await AuthenticateAsync(owned, request, cancellationToken).ConfigureAwait(false) is not { } current || current.GetId() != account.GetId())
            {
                return this.Unauthorized(new { error = "login" });
            }

            var nowUtc = this._clock.GetUtcNow().UtcDateTime;
            if (action == "purchase")
            {
                if (!await VipPendingCoins.ApplyAsync(owned, current, nowUtc, () => owned.SaveChangesAsync(cancellationToken)).ConfigureAwait(false))
                {
                    return await this.ReplyAsync(configuration, wallet, owned, current, request.OperationId, "Pending").ConfigureAwait(false);
                }

                var result = await wallet.PurchaseAsync(owned, current, request.OperationId!.Value, request.LevelId, request.Days, nowUtc, () => owned.SaveChangesAsync(cancellationToken), request.OfferToken, cancellationToken).ConfigureAwait(false);
                return await this.ReplyAsync(configuration, wallet, owned, current, request.OperationId, result.Status, result.Rule).ConfigureAwait(false);
            }

            var rewards = new VipDailyCoins(persistence, game, wallet, this._state, logger);
            var daily = await rewards.ClaimAsync(owned, current, nowUtc, cancellationToken).ConfigureAwait(false);
            if (daily.Status is "DailyApplied" or "AlreadyClaimed")
            {
                try
                {
                    await VipPendingCoins.ApplyAsync(owned, current, nowUtc, () => owned.SaveChangesAsync(cancellationToken)).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "VIP daily credit is durable and awaits normal account coin application.");
                }
            }

            return await this.ReplyAsync(configuration, wallet, owned, current, null, daily.Status).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "The own-account VIP operation could not be confirmed.");
            return this.StatusCode(503, new { error = "vip_unavailable" });
        }
        finally
        {
            if (login is not null)
            {
                try
                {
                    await loginServer.ReleaseAccountReservationAsync(login, reservation).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "The VIP account reservation needs its normal timeout recovery.");
                }
            }
        }
    }

    private async Task<IActionResult> ReplyAsync(VipConfiguration configuration, VipWallet wallet, IPlayerContext context, Account account, Guid? operation, string? status = null, VipRuleResult? rule = null)
    {
        var json = await VipStateWriter.WriteAsync(configuration, this._state, wallet, context, account, this._clock.GetUtcNow().UtcDateTime, operation, status, rule).ConfigureAwait(false);
        return this.Content(json, "application/json");
    }
}
