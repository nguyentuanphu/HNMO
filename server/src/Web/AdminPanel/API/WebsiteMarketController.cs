// <copyright file="WebsiteMarketController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.CashShop;
using HNMO.GameLogic.PlugIns.AccountSafety;
using HNMO.GameLogic.PlugIns.ItemMarket;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.Auth;
using HNMO.Web.AdminPanel.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// Item Market website and authenticated account endpoints:
/// public browse (<c>GET /api/website/market</c>), account market view/coins (<c>POST /api/website/market/account</c>),
/// atomic website purchase (<c>POST /api/website/market/buy</c>), and seller cancel (<c>POST /api/website/market/cancel</c>).
/// </summary>
[ApiController]
[Route("api/website/market")]
public sealed class WebsiteMarketController(
    IPersistenceContextProvider persistence,
    IDataSource<GameConfiguration> gameConfigurationSource,
    IServerProvider serverProvider,
    ILogger<WebsiteMarketController> logger,
    ILoginServer? loginServer = null) : ControllerBase
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> PurchaseGates = new(StringComparer.Ordinal);

    private readonly CashShopCoinService _coinService = new(persistence, gameConfigurationSource);

    /// <summary>Gets the public item market listings and rules.</summary>
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

        engine.ExpireDueListings(DateTime.UtcNow);
        return this.Content(MarketStateWriter.WritePublic(engine, DateTime.UtcNow), "application/json");
    }

    /// <summary>Gets an account's market listings, escrow items, pending Zen, and coin balances.</summary>
    /// <param name="request">Credentials.</param>
    /// <returns>Market state plus coin balances.</returns>
    [HttpPost("account")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.ApiSchemes, Policy = AdminPolicies.AuthenticatedOperator)]
    public async Task<IActionResult> AccountAsync([FromBody] MarketAccountRequest request)
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

        engine.ExpireDueListings(DateTime.UtcNow);
        var coins = await this.GetEffectiveCoinsAsync(context, account, cancellationToken).ConfigureAwait(false);
        var stateJson = MarketStateWriter.WriteAccountState(engine, account.LoginName, bagItems: null, DateTime.UtcNow);
        return this.Content($"{{\"v\":1,\"state\":{stateJson},\"coins\":{{\"wCoinC\":{coins.WCoinC},\"wCoinP\":{coins.WCoinP},\"goblinPoints\":{coins.GoblinPoints}}}}}", "application/json");
    }

    /// <summary>Atomically purchases a coin listing from the website into the buyer's escrow delivery queue.</summary>
    /// <param name="request">Credentials and listing identifier.</param>
    /// <returns>Updated account state and result.</returns>
    [HttpPost("buy")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.ApiSchemes, Policy = AdminPolicies.AuthenticatedOperator)]
    public async Task<IActionResult> BuyAsync([FromBody] MarketActionRequest request)
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

        if (string.IsNullOrWhiteSpace(request.ListingId))
        {
            return this.BadRequest(new { error = "listing" });
        }

        // The market's one-off access fee is paid in the game (F6).
        if (!engine.HasAccess(account.LoginName))
        {
            return this.StatusCode(403, new { error = "access", fee = engine.Configuration.AccessFee, coin = engine.Configuration.AccessFeeCoinType.ToString() });
        }

        // One purchase of an account at a time: two requests at once would both see the same balance and
        // both get their item, while the second debit finds nothing left to take.
        var gate = PurchaseGates.GetOrAdd(account.LoginName.ToUpperInvariant(), static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        // While the account plays, a purchase in the game could spend the same coins at the same moment (audit 1.5):
        // the account is held at the login server for the purchase, as the VIP and gift box pages do. Playing, the
        // player buys in the game (F6).
        var reservation = System.Security.Cryptography.RandomNumberGenerator.GetInt32(byte.MaxValue + 1, int.MaxValue);
        var reserved = false;
        try
        {
            if (loginServer is not null)
            {
                reserved = await loginServer.TryReserveAccountAsync(account.LoginName, reservation).AsTask().WaitAsync(cancellationToken).ConfigureAwait(false);
                if (!reserved)
                {
                    return this.Conflict(new { error = "online" });
                }
            }

            return await this.BuyLockedAsync(engine, context, account, request.ListingId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (reserved)
            {
                await loginServer!.ReleaseAccountReservationAsync(account.LoginName, reservation).ConfigureAwait(false);
            }

            gate.Release();
        }
    }

    private async Task<IActionResult> BuyLockedAsync(MarketEngine engine, IPlayerContext context, Account account, string listingId, CancellationToken cancellationToken)
    {
        var claim = engine.TryClaimForBuy(account.LoginName, listingId, DateTime.UtcNow);
        if (claim.Result != MarketClaimResult.Claimed || claim.Listing is null)
        {
            var err = claim.Result == MarketClaimResult.CannotBuyOwnListing ? "own" : "sold";
            return await this.ActionResponseAsync(engine, context, account, err, null, null, cancellationToken).ConfigureAwait(false);
        }

        var listing = claim.Listing;
        if (listing.Currency == MarketCurrency.Zen)
        {
            engine.RestoreClaimedListing(listing);
            return await this.ActionResponseAsync(
                engine,
                context,
                account,
                "zenGameOnly",
                "Zen listings must be bought in-game (F6).",
                "Vật phẩm bán bằng Zen chỉ có thể mua trong game (F6).",
                cancellationToken).ConfigureAwait(false);
        }

        var coinType = ToCoinType(listing.Currency);
        if (await this.FindOnlinePlayerAsync(account.LoginName).ConfigureAwait(false) is { } liveBuyer)
        {
            await liveBuyer.ApplyPendingCashShopCoinGrantsAsync().ConfigureAwait(false);
        }

        var balances = await this.GetEffectiveCoinsAsync(context, account, cancellationToken).ConfigureAwait(false);
        var available = coinType switch
        {
            CashShopCoinType.WCoinC => balances.WCoinC,
            CashShopCoinType.WCoinP => balances.WCoinP,
            _ => balances.GoblinPoints,
        };

        if (available < listing.Price)
        {
            engine.RestoreClaimedListing(listing);
            return await this.ActionResponseAsync(
                engine,
                context,
                account,
                "InsufficientFunds",
                $"Not enough {listing.Currency} ({listing.Price:N0} needed).",
                $"Không đủ {listing.Currency} (cần {listing.Price:N0}).",
                cancellationToken).ConfigureAwait(false);
        }

        var (buyerStatus, _) = await this._coinService.GrantAsync(
            account.LoginName,
            coinType,
            -listing.Price,
            CashShopBalanceReceipt.Describe(CashShopBalanceReason.MarketPurchase),
            $"market-buy:{listing.Id}",
            "HNMO Item Market",
            cancellationToken).ConfigureAwait(false);

        if (buyerStatus is not (CashShopCoinGrantStatus.Created or CashShopCoinGrantStatus.AlreadyGranted))
        {
            engine.RestoreClaimedListing(listing);
            return await this.ActionResponseAsync(
                engine,
                context,
                account,
                "PaymentFailed",
                "Could not deduct coins; purchase rolled back.",
                "Không thể trừ coin; giao dịch đã hoàn tác.",
                cancellationToken).ConfigureAwait(false);
        }

        // The buyer has paid: from here the purchase is always recorded, even when the seller's payout fails (it
        // is then logged with its reference, so a later grant of the same sale counts once).
        if (claim.NetAmount > 0)
        {
            try
            {
                var (sellerStatus, _) = await this._coinService.GrantAsync(
                    listing.SellerLogin,
                    coinType,
                    claim.NetAmount,
                    CashShopBalanceReceipt.Describe(CashShopBalanceReason.MarketSale),
                    $"market:{listing.Id}",
                    "HNMO Item Market",
                    CancellationToken.None).ConfigureAwait(false);
                if (sellerStatus is not (CashShopCoinGrantStatus.Created or CashShopCoinGrantStatus.AlreadyGranted))
                {
                    logger.LogError("Item Market (website): the seller {Seller} was not paid {Amount} {Coin} for listing {ListingId} ({Status}); grant it with the reference market:{ListingId}.", listing.SellerLogin, claim.NetAmount, coinType, listing.Id, sellerStatus, listing.Id);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Item Market (website): the seller {Seller} was not paid for listing {ListingId}; grant it with the reference market:{ListingId}.", listing.SellerLogin, listing.Id, listing.Id);
            }
        }

        engine.FinalizePurchase(
            account.LoginName,
            claim,
            "website",
            DateTime.UtcNow,
            enqueueItemDelivery: true,
            enqueueZenPayout: false);

        logger.LogInformation("Item Market (website): {Buyer} bought {Item} ({ListingId}) from {Seller} for {Price} {Currency}.", account.LoginName, listing.Item.NameEn, listing.Id, listing.SellerLogin, listing.Price, listing.Currency);

        await this.RefreshOnlinePlayersAsync(account.LoginName, listing.SellerLogin).ConfigureAwait(false);
        return await this.ActionResponseAsync(
            engine,
            context,
            account,
            "Bought",
            $"Bought {listing.Item.NameEn}! Claim it in-game via Market (F6).",
            $"Đã mua {listing.Item.NameVi}! Vào game bấm Chợ (F6) để nhận.",
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Cancels a seller's listing from the website and moves the item to escrow delivery.</summary>
    /// <param name="request">Credentials and listing identifier.</param>
    /// <returns>Updated account state and result.</returns>
    [HttpPost("cancel")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.ApiSchemes, Policy = AdminPolicies.AuthenticatedOperator)]
    public async Task<IActionResult> CancelAsync([FromBody] MarketActionRequest request)
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

        if (string.IsNullOrWhiteSpace(request.ListingId))
        {
            return this.BadRequest(new { error = "listing" });
        }

        if (!engine.CancelListing(account.LoginName, request.ListingId, isAdmin: false, "website", DateTime.UtcNow))
        {
            return await this.ActionResponseAsync(
                engine,
                context,
                account,
                "CancelFailed",
                "Listing not found or no longer active.",
                "Không tìm thấy tin đăng.",
                cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation("Item Market (website): {Seller} canceled listing {ListingId}.", account.LoginName, request.ListingId);
        await this.RefreshOnlinePlayersAsync(account.LoginName, null).ConfigureAwait(false);
        return await this.ActionResponseAsync(
            engine,
            context,
            account,
            "Canceled",
            "Listing canceled. Claim your item in-game via Market (F6).",
            "Đã hủy tin đăng. Vào game bấm Chợ (F6) để nhận lại đồ.",
            cancellationToken).ConfigureAwait(false);
    }

    private static CashShopCoinType ToCoinType(MarketCurrency currency) => currency switch
    {
        MarketCurrency.WCoinC => CashShopCoinType.WCoinC,
        MarketCurrency.WCoinP => CashShopCoinType.WCoinP,
        _ => CashShopCoinType.GoblinPoints,
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

    private async Task<IActionResult> ActionResponseAsync(
        MarketEngine engine,
        IPlayerContext context,
        Account account,
        string result,
        string? messageEn,
        string? messageVi,
        CancellationToken cancellationToken)
    {
        var coins = await this.GetEffectiveCoinsAsync(context, account, cancellationToken).ConfigureAwait(false);
        var stateJson = MarketStateWriter.WriteAccountState(engine, account.LoginName, bagItems: null, DateTime.UtcNow, result, messageEn, messageVi);
        return this.Content($"{{\"v\":1,\"result\":\"{result}\",\"state\":{stateJson},\"coins\":{{\"wCoinC\":{coins.WCoinC},\"wCoinP\":{coins.WCoinP},\"goblinPoints\":{coins.GoblinPoints}}}}}", "application/json");
    }

    private async Task<(int WCoinC, int WCoinP, int GoblinPoints)> GetEffectiveCoinsAsync(
        IPlayerContext context,
        Account account,
        CancellationToken cancellationToken)
    {
        var live = await this.FindOnlinePlayerAsync(account.LoginName).ConfigureAwait(false);
        var baseC = live?.Account?.WCoinC ?? account.WCoinC;
        var baseP = live?.Account?.WCoinP ?? account.WCoinP;
        var baseG = live?.Account?.GoblinPoints ?? account.GoblinPoints;

        var pending = await context.GetPendingCashShopCoinGrantsAsync(account.GetId(), cancellationToken).ConfigureAwait(false);
        var c = Math.Max(0L, baseC + pending.Where(g => g.CoinType == CashShopCoinType.WCoinC).Sum(g => (long)g.Amount));
        var p = Math.Max(0L, baseP + pending.Where(g => g.CoinType == CashShopCoinType.WCoinP).Sum(g => (long)g.Amount));
        var g = Math.Max(0L, baseG + pending.Where(g => g.CoinType == CashShopCoinType.GoblinPoints).Sum(g => (long)g.Amount));
        return ((int)Math.Min(int.MaxValue, c), (int)Math.Min(int.MaxValue, p), (int)Math.Min(int.MaxValue, g));
    }

    private async Task<Player?> FindOnlinePlayerAsync(string login)
    {
        foreach (var ctx in serverProvider.Servers.OfType<IGameServerContextProvider>().Select(s => s.Context).Distinct())
        {
            var players = await ctx.GetPlayersAsync().ConfigureAwait(false);
            var hit = players.FirstOrDefault(p => string.Equals(p.Account?.LoginName, login, StringComparison.OrdinalIgnoreCase));
            if (hit is not null)
            {
                return hit;
            }
        }

        return null;
    }

    private async Task RefreshOnlinePlayersAsync(string buyerLogin, string? sellerLogin)
    {
        foreach (var ctx in serverProvider.Servers.OfType<IGameServerContextProvider>().Select(s => s.Context).Distinct())
        {
            var plugIn = MarketPlugIn.Get(ctx);
            var players = await ctx.GetPlayersAsync().ConfigureAwait(false);
            foreach (var p in players)
            {
                var name = p.Account?.LoginName;
                if (name is null)
                {
                    continue;
                }

                if (string.Equals(name, buyerLogin, StringComparison.OrdinalIgnoreCase)
                    || (sellerLogin is not null && string.Equals(name, sellerLogin, StringComparison.OrdinalIgnoreCase)))
                {
                    await p.ApplyPendingCashShopCoinGrantsAsync().ConfigureAwait(false);
                    if (plugIn is not null)
                    {
                        await plugIn.SendStateAsync(p, requested: false).ConfigureAwait(false);
                    }
                }
            }
        }
    }

    private async Task<MarketEngine?> EngineAsync(CancellationToken cancellationToken)
    {
        var configuration = await PlugInSettings.ReadActiveAsync<MarketConfiguration>(persistence, typeof(MarketPlugIn), () => new MarketConfiguration(), cancellationToken).ConfigureAwait(false);
        return configuration is null ? null : new MarketEngine(configuration, MarketStore.Open(configuration.FileName), enabled: true);
    }
}
