// <copyright file="WebsiteGiftBoxController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.GameLogic.PlugIns.GiftBox;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>Own-account mailbox access through the authenticated bridge, with exclusive offline collection.</summary>
/// <param name="persistence">The account persistence provider.</param>
/// <param name="configurations">The canonical game definitions.</param>
/// <param name="loginServer">The authoritative account lease service.</param>
/// <param name="logger">The operational logger; credentials and message bodies are never logged.</param>
/// <param name="rows">An optional isolated mailbox backend.</param>
/// <param name="clock">The authoritative clock.</param>
[ApiController]
[Route("api/website/account/gift-box")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.ApiSchemes, Policy = AdminPolicies.AuthenticatedOperator)]
[RequestSizeLimit(2048)]
public sealed class WebsiteGiftBoxController(IPersistenceContextProvider persistence, IDataSource<GameConfiguration> configurations, ILoginServer loginServer, ILogger<WebsiteGiftBoxController> logger, IHnmoPlayerStateRowBackend? rows = null, TimeProvider? clock = null) : ControllerBase
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly IHnmoPlayerStateRowBackend? _injectedRows = rows;
    private IHnmoPlayerStateRowBackend? _rows = rows;

    private IHnmoPlayerStateRowBackend Rows => this._rows ??= HnmoPlayerStateRowStoreRegistry.GetBackend(Path.Combine(AppContext.BaseDirectory, "save", HnmoStateStore.StateFileName));

    /// <summary>Reads only the authenticated account's bounded inbox and selected public letter.</summary>
    /// <param name="request">Current credentials and the bounded display selection.</param>
    /// <returns>Safe localized mailbox display fields.</returns>
    [HttpPost("state")]
    public Task<IActionResult> StateAsync([FromBody] WebsiteGiftBoxRequest request) => this.HandleAsync(request, false);

    /// <summary>Collects one existing owed letter for an owned character under an offline lease.</summary>
    /// <param name="request">Current credentials, letter and owned character selection.</param>
    /// <returns>The safe mailbox and a machine outcome translated by the client.</returns>
    [HttpPost("collect")]
    public Task<IActionResult> CollectAsync([FromBody] WebsiteGiftBoxRequest request) => this.HandleAsync(request, true);

    private static async ValueTask<Account?> AuthenticateAsync(IPlayerContext context, WebsiteGiftBoxRequest request, CancellationToken token)
    {
        if (request.LoginName is not { Length: > 0 and <= 10 } login || request.Password is not { Length: > 0 and <= 20 } password)
        {
            return null;
        }

        var account = await context.GetAccountByLoginNameAsync(login, token).ConfigureAwait(false);
        return account is { IsBot: false, State: not AccountState.Banned and not AccountState.TemporarilyBanned, PasswordHash.Length: > 0 }
            && BCrypt.Net.BCrypt.Verify(password, account.PasswordHash) ? account : null;
    }

    private async Task<IActionResult> HandleAsync(WebsiteGiftBoxRequest request, bool collect)
    {
        this.Response.Headers.CacheControl = "no-store";
        var selection = new GiftBoxRequest(collect ? "collect" : "state", request.Offset, request.LetterId, request.CharacterId, request.Language);
        if (!selection.IsValid)
        {
            return this.BadRequest(new { error = "gift_box_request" });
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(this.HttpContext.RequestAborted);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        var token = deadline.Token;
        var reservation = RandomNumberGenerator.GetInt32(byte.MaxValue + 1, int.MaxValue);
        string? login = null;
        try
        {
            var game = await configurations.GetOwnerAsync(Guid.Empty, token).ConfigureAwait(false);
            var box = new GiftBoxDispatch(this.Rows);
            using var initial = persistence.CreateNewPlayerContext(game);
            if (await AuthenticateAsync(initial, request, token).ConfigureAwait(false) is not { } account)
            {
                return this.Unauthorized(new { error = "login" });
            }

            if (!collect)
            {
                return await this.ReplyAsync(box, game, initial, account, selection).ConfigureAwait(false);
            }

            login = account.LoginName;
            if (!await loginServer.TryReserveAccountAsync(login, reservation).AsTask().WaitAsync(token).ConfigureAwait(false))
            {
                return await this.ReplyAsync(box, game, initial, account, selection, "Online").ConfigureAwait(false);
            }

            using var owned = persistence.CreateNewPlayerContext(game);
            if (await AuthenticateAsync(owned, request, token).ConfigureAwait(false) is not { } current || current.GetId() != account.GetId())
            {
                return this.Unauthorized(new { error = "login" });
            }

            token.ThrowIfCancellationRequested();
            // A season whose cash shop is off gives no coins from letters (owner 2026-11-28, classic play).
            var (_, cashShopOn, _) = await PlugInSettings.ReadStoredAsync<HNMO.GameLogic.CashShop.CashShopSettings>(persistence, typeof(HNMO.GameLogic.CashShop.CashShopFeaturePlugIn), token).ConfigureAwait(false);
            var collection = new GiftBoxCollection(box, game, logger, cashShopOn);
            var now = this._clock.GetUtcNow().UtcDateTime;
            var result = await collection.CollectAsync(owned, current, selection.LetterId!.Value, selection.CharacterId!.Value, now, () => owned.SaveChangesAsync(token)).ConfigureAwait(false);
            if (this._injectedRows is null && result.Status == "Applied" && result.Receipt is { } receipt)
            {
                try
                {
                    var receiver = current.Characters.FirstOrDefault(c => c.GetId() == receipt.CharacterId)?.Name ?? string.Empty;
                    var details = JsonSerializer.Serialize(new { kind = "gift-box-collect", source = "website", letterId = receipt.LetterId, coins = receipt.Coins, coinType = receipt.CoinType.ToString(), zen = receipt.Zen, items = receipt.ItemIds.Count });
                    ActivityLogStore.Open(Path.Combine(AppContext.BaseDirectory, "save", HnmoStateStore.StateFileName)).Append(new ActivityLogEntry
                    {
                        TimestampUtc = now,
                        Kind = ActivityKind.Administration,
                        Account = current.LoginName,
                        Character = receiver,
                        ItemSummary = receipt.TitleEn,
                        Details = details,
                    });
                }
                catch
                {
                    // Non-fatal: the reward is already durably saved with its receipt.
                }
            }

            return await this.ReplyAsync(box, game, owned, current, selection, result.Status).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "The own-account gift collection could not be confirmed.");
            return this.StatusCode(503, new { error = "gift_box_unavailable" });
        }
        finally
        {
            if (login is not null)
            {
                try
                {
                    // Also release after a lost reservation response, using only this request's unique lease ID.
                    await loginServer.ReleaseAccountReservationAsync(login, reservation).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "The gift collection lease needs its normal timeout recovery.");
                }
            }
        }
    }

    private async Task<IActionResult> ReplyAsync(GiftBoxDispatch box, GameConfiguration game, IPlayerContext context, Account account, GiftBoxRequest request, string? status = null)
    {
        var json = await GiftBoxStateWriter.WriteAsync(box, game, context, account, request, this._clock.GetUtcNow().UtcDateTime, status).ConfigureAwait(false);
        return this.Content(json, "application/json");
    }
}
