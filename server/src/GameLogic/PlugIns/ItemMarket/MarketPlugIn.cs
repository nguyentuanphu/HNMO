// <copyright file="MarketPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ItemMarket;

using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.Administration;
using HNMO.GameLogic.CashShop;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlugIns.AccountSafety;
using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.GameLogic.PlugIns.PeriodicTasks;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.Inventory;
using HNMO.Interfaces;
using HNMO.PlugIns;

/// <summary>
/// Escrow Item Market plug-in (OFF by default): list bag items for Zen or CashShop coins (<kbd>F6</kbd>, Market Julia NPC, or website),
/// atomic concurrent purchase handling, market fee, banned item filters, and full audit logging.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Item Market", Description = "Atomic escrow marketplace for buying and selling items with Zen or coins in-game and on the website (OFF by default).")]
[Guid("A41D9B83-27F6-409C-B1E4-5C83D2F6E790")]
public sealed class MarketPlugIn :
    IPeriodicTaskPlugIn,
    IPlayerStateChangedPlugIn,
    IPlayerTalkToNpcPlugIn,
    ISupportCustomConfiguration<MarketConfiguration>,
    ISupportDefaultCustomConfiguration,
    IDisabledByDefault
{
    private readonly SemaphoreSlim _accessGate = new(1, 1);
    private (MarketConfiguration Config, MarketEngine Engine)? _engine;
    private DateTime _lastExpireUtc = DateTime.MinValue;

    /// <inheritdoc />
    public MarketConfiguration? Configuration { get; set; }

    /// <summary>Gets the active <see cref="MarketPlugIn"/> for a game context, or <c>null</c> when disabled.</summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The active plug-in, or <c>null</c>.</returns>
    public static MarketPlugIn? Get(IGameContext gameContext) =>
        gameContext.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<MarketPlugIn>().FirstOrDefault();

    /// <summary>Captures a portable item snapshot from an inventory item entity.</summary>
    /// <param name="item">The source item.</param>
    /// <returns>The item snapshot.</returns>
    public static MarketItemSnapshot SnapshotFromItem(Item item)
    {
        var def = item.Definition!;
        var optionLevel = (byte)(item.ItemOptions.FirstOrDefault(o => o.ItemOption?.OptionType == ItemOptionTypes.Option)?.Level ?? 0);
        var hasLuck = item.ItemOptions.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.Luck);
        var exMask = (byte)item.ItemOptions
            .Where(o => o.ItemOption?.OptionType == ItemOptionTypes.Excellent && o.ItemOption.Number is >= 1 and <= 8)
            .Aggregate(0, (mask, o) => mask | (1 << (o.ItemOption!.Number - 1)));
        var en = def.Name.GetTranslation(CultureInfo.GetCultureInfo("en")) ?? def.Name.ValueInNeutralLanguage;
        var vi = def.Name.GetTranslation(CultureInfo.GetCultureInfo("vi")) ?? en;

        return new MarketItemSnapshot
        {
            Group = def.Group,
            Number = def.Number,
            Level = item.Level,
            Durability = (byte)Math.Clamp((int)item.Durability, 0, 255),
            Skill = item.HasSkill,
            Luck = hasLuck,
            OptionLevel = optionLevel,
            ExcellentOptions = exMask,
            NameEn = en,
            NameVi = vi,
        };
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new MarketConfiguration();

    /// <summary>Gets or creates the market engine for the current configuration.</summary>
    /// <returns>The engine.</returns>
    public MarketEngine GetEngine()
    {
        var cfg = this.Configuration ??= new MarketConfiguration();
        if (this._engine is { } cached && ReferenceEquals(cached.Config, cfg))
        {
            return cached.Engine;
        }

        var engine = new MarketEngine(cfg, MarketStore.Open(cfg.FileName), enabled: true);
        this._engine = (cfg, engine);
        return engine;
    }

    /// <inheritdoc />
    public ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var utcNow = DateTime.UtcNow;
        if (utcNow - this._lastExpireUtc < TimeSpan.FromMinutes(1))
        {
            return ValueTask.CompletedTask;
        }

        this._lastExpireUtc = utcNow;
        this.GetEngine().ExpireDueListings(utcNow);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (currentState == PlayerState.EnteredWorld && player.SelectedCharacter is not null)
        {
            await this.SendStateAsync(player, requested: false).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask PlayerTalksToNpcAsync(Player player, NonPlayerCharacter npc, NpcTalkEventArgs eventArgs)
    {
        var cfg = this.Configuration ?? new MarketConfiguration();
        if (npc.Definition.Number != cfg.MarketNpcNumber || cfg.MarketNpcNumber <= 0)
        {
            return;
        }

        eventArgs.HasBeenHandled = true;
        await this.SendStateAsync(player, requested: true).ConfigureAwait(false);
    }

    /// <summary>Sends the player's Item Market snapshot via <c>F5 39</c>.</summary>
    /// <param name="player">The player.</param>
    /// <param name="requested">Whether explicitly requested by the client.</param>
    /// <param name="actionResult">Optional action result code.</param>
    /// <param name="messageEn">Optional English notice.</param>
    /// <param name="messageVi">Optional Vietnamese notice.</param>
    /// <returns>A task representing the send.</returns>
    public async ValueTask SendStateAsync(
        Player player,
        bool requested,
        string? actionResult = null,
        string? messageEn = null,
        string? messageVi = null)
    {
        var login = player.Account?.LoginName;
        if (string.IsNullOrWhiteSpace(login))
        {
            return;
        }

        var bagItems = CollectBagItems(player);
        var json = MarketStateWriter.WriteAccountState(this.GetEngine(), login, bagItems, DateTime.UtcNow, actionResult, messageEn, messageVi);
        await player.InvokeViewPlugInAsync<IHnmoMarketViewPlugIn>(p => p.ShowMarketAsync(json, requested)).ConfigureAwait(false);
    }

    /// <summary>Moves a bag item into escrow and lists it on the market.</summary>
    /// <param name="player">The seller.</param>
    /// <param name="slot">Inventory bag slot.</param>
    /// <param name="currency">Requested currency.</param>
    /// <param name="price">Asking price.</param>
    /// <returns>A task representing the listing.</returns>
    public async ValueTask ListItemAsync(Player player, byte slot, MarketCurrency currency, int price)
    {
        var login = player.Account?.LoginName;
        var charName = player.SelectedCharacter?.Name;
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(charName) || player.Inventory is null)
        {
            return;
        }

        var engine = this.GetEngine();
        if (!await this.EnsureSafeZoneAsync(player, engine).ConfigureAwait(false))
        {
            return;
        }

        if (!await this.EnsureAccessAsync(player, engine, login).ConfigureAwait(false))
        {
            return;
        }

        var item = player.Inventory.GetItem(slot);
        if (item?.Definition is null || slot < InventoryConstants.EquippableSlotsCount || slot >= InventoryConstants.FirstStoreItemSlotIndex)
        {
            await this.NotifyAsync(player, "MissingItem", "Pick an item inside your bag (not equipped).", "Hãy chọn vật phẩm trong túi đồ.").ConfigureAwait(false);
            return;
        }

        if (AccountSafetyPlugIn.Get(player.GameContext) is { } safety && await safety.RefuseLockedItemAsync(player, item).ConfigureAwait(false))
        {
            await this.SendStateAsync(player, true).ConfigureAwait(false);
            return;
        }

        if (await Escrow.EscrowContractsPlugIn.RefuseCooldownItemAsync(player, item).ConfigureAwait(false))
        {
            await this.SendStateAsync(player, true).ConfigureAwait(false);
            return;
        }

        var fee = Math.Max(0, engine.Configuration.ListingZenFee);
        if (fee > 0 && player.Money < fee)
        {
            await this.NotifyAsync(player, "NotEnoughZen", $"Need {fee:N0} Zen listing fee.", $"Cần {fee:N0} Zen phí đăng bán.").ConfigureAwait(false);
            return;
        }

        // The real item leaves the bag into a storage of its own; the listing keeps that storage's id, so the buyer
        // gets this very item with all its details. Listing, fee and move are saved together; anything failing on
        // the way puts everything back.
        var snapshot = SnapshotFromItem(item);
        var held = await HeldItems.HoldAsync(player, item).ConfigureAwait(false);
        if (held is null || held.Id == Guid.Empty)
        {
            await this.NotifyAsync(player, "ListFailed", "The item could not be listed; it stays in your bag.", "Không thể đăng bán; vật phẩm vẫn ở trong túi đồ.").ConfigureAwait(false);
            return;
        }

        snapshot.StorageId = held.Id;
        var listResult = engine.CreateListing(login, charName, snapshot, currency, price, DateTime.UtcNow, out var listing);
        if (listResult != MarketListResult.Success || listing is null)
        {
            await HeldItems.ReleaseAsync(player, held).ConfigureAwait(false);
            var (en, vi) = listResult switch
            {
                MarketListResult.ItemBanned => ($"{snapshot.NameEn} is banned from listing on the market.", $"{snapshot.NameVi} nằm trong danh sách cấm rao bán."),
                MarketListResult.CurrencyDisabled => ("That currency is disabled on this server.", "Loại tiền tệ này hiện bị tắt."),
                MarketListResult.InvalidPrice => ("Enter a price greater than 0.", "Giá bán phải lớn hơn 0."),
                MarketListResult.ListingLimitReached => ("You reached the maximum active market listings.", "Bạn đã đạt số lượng tin đăng tối đa."),
                _ => ("Cannot list this item.", "Không thể đăng bán vật phẩm này."),
            };
            await this.NotifyAsync(player, listResult.ToString(), en, vi).ConfigureAwait(false);
            return;
        }

        var feePaid = fee > 0 && player.TryRemoveMoney(fee);
        bool saved;
        try
        {
            saved = await player.SaveProgressAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Market: listing {ListingId} couldn't be saved.", listing.Id);
            saved = false;
        }

        if (!saved)
        {
            engine.WithdrawListing(listing.Id);
            await HeldItems.ReleaseAsync(player, held).ConfigureAwait(false);
            if (feePaid)
            {
                player.TryAddMoney(fee);
            }

            await this.NotifyAsync(player, "ListFailed", "The item could not be listed; it stays in your bag.", "Không thể đăng bán; vật phẩm vẫn ở trong túi đồ.").ConfigureAwait(false);
            return;
        }

        if (feePaid)
        {
            await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
        }

        await this.NotifyAsync(player, "Listed", $"Listed {snapshot.NameEn} for {price:N0} {currency}!", $"Đã đăng bán {snapshot.NameVi} với giá {price:N0} {currency}!").ConfigureAwait(false);
    }

    /// <summary>Atomically claims and purchases an active market listing in-game.</summary>
    /// <param name="player">The buyer.</param>
    /// <param name="listingId">Listing identifier.</param>
    /// <returns>A task representing the purchase.</returns>
    public async ValueTask BuyAsync(Player player, string listingId)
    {
        var login = player.Account?.LoginName;
        if (string.IsNullOrWhiteSpace(login) || player.Account is null)
        {
            return;
        }

        var engine = this.GetEngine();
        if (!await this.EnsureSafeZoneAsync(player, engine).ConfigureAwait(false))
        {
            return;
        }

        if (!await this.EnsureAccessAsync(player, engine, login, seller: false).ConfigureAwait(false))
        {
            return;
        }

        var claim = engine.TryClaimForBuy(login, listingId, DateTime.UtcNow);
        if (claim.Result != MarketClaimResult.Claimed || claim.Listing is null)
        {
            var (en, vi) = claim.Result == MarketClaimResult.CannotBuyOwnListing
                ? ("You cannot buy your own listing.", "Bạn không thể mua đồ của chính mình.")
                : ("That listing was already sold or removed.", "Vật phẩm đã có người mua hoặc bị hủy.");
            await this.NotifyAsync(player, claim.Result.ToString(), en, vi).ConfigureAwait(false);
            return;
        }

        var listing = claim.Listing;
        var paidOk = await TryPayBuyerAsync(player, listing.Currency, listing.Price).ConfigureAwait(false);
        if (!paidOk)
        {
            engine.RestoreClaimedListing(listing);
            await this.NotifyAsync(player, "InsufficientFunds", $"Not enough {listing.Currency} ({listing.Price:N0} needed).", $"Không đủ {listing.Currency} (cần {listing.Price:N0}).").ConfigureAwait(false);
            return;
        }

        // Payout seller coins immediately via MissionCoinGranter (works online or offline); Zen via PendingZen.
        if (listing.Currency != MarketCurrency.Zen && claim.NetAmount > 0)
        {
            var coinType = listing.Currency switch
            {
                MarketCurrency.WCoinC => CashShopCoinType.WCoinC,
                MarketCurrency.WCoinP => CashShopCoinType.WCoinP,
                _ => CashShopCoinType.GoblinPoints,
            };
            var granted = await MissionCoinGranter.GrantAsync(
                player.GameContext,
                new CoinGrant(listing.SellerLogin, coinType, claim.NetAmount, CashShopBalanceReceipt.Describe(CashShopBalanceReason.MarketSale), $"market:{listing.Id}"),
                "HNMO Item Market").ConfigureAwait(false);
            if (!granted)
            {
                // The buyer has paid and gets the item; the seller's coins are owed. The reference makes a later
                // grant of the same sale count once.
                player.Logger.LogError("Market: the seller {Seller} was not paid {Amount} {Coin} for listing {ListingId}; grant it by hand with the reference market:{ListingId}.", listing.SellerLogin, claim.NetAmount, coinType, listing.Id, listing.Id);
            }
        }

        // Try immediate bag delivery; fall back to the market's claim list if the bag is full or the delivery fails,
        // so the paid purchase is always recorded.
        bool deliveredDirectly;
        try
        {
            deliveredDirectly = await GiveItemAsync(player, listing.Item, listing.Id).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Market: listing {ListingId} bought, the item goes to the claim list.", listing.Id);
            deliveredDirectly = false;
        }

        engine.FinalizePurchase(
            login,
            claim,
            "game",
            DateTime.UtcNow,
            enqueueItemDelivery: !deliveredDirectly,
            enqueueZenPayout: listing.Currency == MarketCurrency.Zen);

        var enDone = deliveredDirectly
            ? $"Bought {listing.Item.NameEn} for {listing.Price:N0} {listing.Currency}!"
            : $"Bought {listing.Item.NameEn}! Bag was full; claim it in the Market window.";
        var viDone = deliveredDirectly
            ? $"Đã mua {listing.Item.NameVi} giá {listing.Price:N0} {listing.Currency}!"
            : $"Đã mua {listing.Item.NameVi}! Túi đồ đầy, hãy nhận ở mục Kho chợ.";
        await this.NotifyAsync(player, "Bought", enDone, viDone).ConfigureAwait(false);
    }

    /// <summary>Cancels one of the seller's listings and places the item into escrow delivery.</summary>
    /// <param name="player">The seller.</param>
    /// <param name="listingId">Listing identifier.</param>
    /// <returns>A task representing cancellation.</returns>
    public async ValueTask CancelAsync(Player player, string listingId)
    {
        var login = player.Account?.LoginName;
        if (string.IsNullOrWhiteSpace(login))
        {
            return;
        }

        var engine = this.GetEngine();
        if (!engine.CancelListing(login, listingId, isAdmin: false, "game", DateTime.UtcNow))
        {
            await this.NotifyAsync(player, "CancelFailed", "Listing not found or no longer active.", "Không tìm thấy tin đăng.").ConfigureAwait(false);
            return;
        }

        await this.ClaimAsync(player).ConfigureAwait(false);
    }

    /// <summary>Delivers pending escrowed items and pending Zen payouts to the online player.</summary>
    /// <param name="player">The player.</param>
    /// <returns>A task representing delivery.</returns>
    public async ValueTask ClaimAsync(Player player)
    {
        var login = player.Account?.LoginName;
        if (string.IsNullOrWhiteSpace(login))
        {
            return;
        }

        var engine = this.GetEngine();
        var deliveries = engine.PeekDeliveries(login);
        var itemsGiven = 0;
        var bagFull = false;
        foreach (var d in deliveries)
        {
            if (await GiveItemAsync(player, d.Item, d.Id).ConfigureAwait(false))
            {
                engine.CompleteDelivery(d.Id);
                itemsGiven++;
            }
            else
            {
                bagFull = true;
                break;
            }
        }

        long zenGiven = 0;
        foreach (var z in engine.PeekZenPayouts(login))
        {
            if (player.TryAddMoney(z.NetZen))
            {
                engine.CompleteZenPayout(z.Id);
                zenGiven += z.NetZen;
            }
        }

        var en = bagFull
            ? $"Claimed {itemsGiven} item(s) and {zenGiven:N0} Zen; free more bag space for the rest."
            : $"Claimed {itemsGiven} item(s) and {zenGiven:N0} Zen from escrow.";
        var vi = bagFull
            ? $"Đã nhận {itemsGiven} vật phẩm và {zenGiven:N0} Zen; hãy dọn thêm túi đồ cho phần còn lại."
            : $"Đã nhận {itemsGiven} vật phẩm và {zenGiven:N0} Zen từ kho chợ.";
        await this.NotifyAsync(player, bagFull ? "BagFull" : "Claimed", en, vi).ConfigureAwait(false);
    }

    /// <summary>Pays one of the market's one-off access fees for the player's account.</summary>
    /// <param name="player">The player.</param>
    /// <param name="seller"><c>true</c> for the seller access (selling and buying), <c>false</c> for buying only.</param>
    /// <returns>A task representing the payment.</returns>
    public async ValueTask PayAccessAsync(Player player, bool seller = true)
    {
        if (player.Account is not { LoginName: { Length: > 0 } login } account)
        {
            return;
        }

        var engine = this.GetEngine();
        var cfg = engine.Configuration;
        if (seller ? engine.HasAccess(login) : engine.HasBuyerAccess(login))
        {
            await this.NotifyAsync(player, "AccessPaid", seller ? "Your account can already sell on this market." : "Your account can already buy on this market.", seller ? "Tài khoản của bạn đã được bán ở chợ này." : "Tài khoản của bạn đã được mua ở chợ này.").ConfigureAwait(false);
            return;
        }

        var fee = seller ? cfg.AccessFee : cfg.BuyerAccessFee;
        await this._accessGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await player.ApplyPendingCashShopCoinGrantsAsync().ConfigureAwait(false);
            var balance = account.GetCashShopCoins(cfg.AccessFeeCoinType);
            if (balance < fee)
            {
                await this.NotifyAsync(player, "AccessNotEnough", $"This costs {fee:N0} {cfg.AccessFeeCoinType}; you have {balance:N0}.", $"Cần {fee:N0} {cfg.AccessFeeCoinType}; bạn có {balance:N0}.").ConfigureAwait(false);
                return;
            }

            account.SetCashShopCoins(cfg.AccessFeeCoinType, balance - fee);
            CashShopBalanceReceipt.Record(player.PersistenceContext, account, cfg.AccessFeeCoinType, -fee, CashShopBalanceReason.MarketAccess);
            if (!await player.SaveProgressAsync().ConfigureAwait(false))
            {
                // Nothing is bought when the payment couldn't be saved: the coins go back.
                account.SetCashShopCoins(cfg.AccessFeeCoinType, account.GetCashShopCoins(cfg.AccessFeeCoinType) + fee);
                await this.NotifyAsync(player, "AccessFailed", "The payment couldn't be saved; no coins were taken. Please try again.", "Không lưu được giao dịch, chưa trừ xu. Vui lòng thử lại.").ConfigureAwait(false);
                return;
            }

            engine.GrantAccess(login, seller);
        }
        finally
        {
            this._accessGate.Release();
        }

        var (en, vi) = seller
            ? ($"Selling unlocked on this market ({fee:N0} {cfg.AccessFeeCoinType}); you can buy too. Every sale pays a {cfg.FeePercent:0.#}% tax.", $"Đã mở quyền bán ở chợ này ({fee:N0} {cfg.AccessFeeCoinType}); bạn cũng mua được. Mỗi lần bán đóng thuế {cfg.FeePercent:0.#}%.")
            : ($"Buying unlocked on this market ({fee:N0} {cfg.AccessFeeCoinType}).", $"Đã mở quyền mua ở chợ này ({fee:N0} {cfg.AccessFeeCoinType}).");
        await this.NotifyAsync(player, "AccessPaid", en, vi).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void ForceStart()
    {
    }

    /// <summary>
    /// Gives the item of a listing or delivery: the real held item when the market keeps one, otherwise (listings made
    /// before) an item made from the record. Returns whether it's in the bag now (a held item is saved there at once).
    /// </summary>
    private static async ValueTask<bool> GiveItemAsync(Player player, MarketItemSnapshot item, string reference)
    {
        if (item.StorageId == Guid.Empty)
        {
            return await GiftCodeRewards.GiveGoodsAsync(player, ToOwedReward(item)).ConfigureAwait(false);
        }

        var result = await HeldItems.GiveAsync(player, item.StorageId).ConfigureAwait(false);
        if (result == HeldItems.GiveResult.Gone)
        {
            // Given before (a delivery can't be paid twice): count it as done, so the list doesn't keep it forever.
            player.Logger.LogWarning("Market: the held item of {Reference} (storage {StorageId}) was given before.", reference, item.StorageId);
            return true;
        }

        return result == HeldItems.GiveResult.Given;
    }

    private static GiftCodeOwedReward ToOwedReward(MarketItemSnapshot s) => new()
    {
        Items =
        [
            new GiftCodeItem
            {
                Group = s.Group,
                Number = s.Number,
                Level = s.Level,
                Durability = s.Durability,
                Count = 1,
                Skill = s.Skill,
                Luck = s.Luck,
                OptionLevel = s.OptionLevel,
                ExcellentOptions = s.ExcellentOptions,
            },
        ],
    };

    private static List<(byte Slot, MarketItemSnapshot Item)> CollectBagItems(Player player)
    {
        var list = new List<(byte, MarketItemSnapshot)>();
        if (player.Inventory is null)
        {
            return list;
        }

        foreach (var item in player.Inventory.Items)
        {
            if (item.ItemSlot < InventoryConstants.EquippableSlotsCount || item.ItemSlot >= InventoryConstants.FirstStoreItemSlotIndex || item.Definition is null)
            {
                continue;
            }

            list.Add((item.ItemSlot, SnapshotFromItem(item)));
        }

        return list.OrderBy(x => x.Item1).ToList();
    }

    private static async ValueTask<bool> TryPayBuyerAsync(Player player, MarketCurrency currency, int price)
    {
        if (currency == MarketCurrency.Zen)
        {
            return player.TryRemoveMoney(price);
        }

        var account = player.Account;
        if (account is null)
        {
            return false;
        }

        await player.ApplyPendingCashShopCoinGrantsAsync().ConfigureAwait(false);
        var coinType = currency switch
        {
            MarketCurrency.WCoinC => CashShopCoinType.WCoinC,
            MarketCurrency.WCoinP => CashShopCoinType.WCoinP,
            _ => CashShopCoinType.GoblinPoints,
        };
        var balance = account.GetCashShopCoins(coinType);
        if (balance < price)
        {
            return false;
        }

        account.SetCashShopCoins(coinType, balance - price);
        var receipt = CashShopBalanceReceipt.Record(player.PersistenceContext, account, coinType, -price, CashShopBalanceReason.MarketPurchase);
        if (!await player.SaveProgressAsync().ConfigureAwait(false))
        {
            account.SetCashShopCoins(coinType, balance);
            await CashShopBalanceReceipt.RemoveAsync(player.PersistenceContext, receipt).ConfigureAwait(false);
            return false;
        }

        return true;
    }

    private async ValueTask<bool> EnsureAccessAsync(Player player, MarketEngine engine, string login, bool seller = true)
    {
        if (seller ? engine.HasAccess(login) : engine.HasBuyerAccess(login))
        {
            return true;
        }

        var cfg = engine.Configuration;
        var (en, vi) = seller
            ? ($"Unlock selling first: a one-off {cfg.AccessFee:N0} {cfg.AccessFeeCoinType} for your account on this season's market.", $"Hãy mở quyền bán trước: phí một lần {cfg.AccessFee:N0} {cfg.AccessFeeCoinType} cho tài khoản ở chợ season này.")
            : ($"Unlock buying first: a one-off {cfg.BuyerAccessFee:N0} {cfg.AccessFeeCoinType} for your account on this season's market.", $"Hãy mở quyền mua trước: phí một lần {cfg.BuyerAccessFee:N0} {cfg.AccessFeeCoinType} cho tài khoản ở chợ season này.");
        await this.NotifyAsync(player, "AccessRequired", en, vi).ConfigureAwait(false);
        return false;
    }

    private async ValueTask<bool> EnsureSafeZoneAsync(Player player, MarketEngine engine)
    {
        if (!engine.Configuration.SafeZoneOnly || player.CurrentMap is null)
        {
            return true;
        }

        if (player.CurrentMap.Terrain.SafezoneMap[player.Position.X, player.Position.Y])
        {
            return true;
        }

        await this.NotifyAsync(player, "NotInSafeZone", "Enter a town safe zone to list or buy market items.", "Hãy vào vùng an toàn trong thành để đăng/mua đồ trên chợ.").ConfigureAwait(false);
        return false;
    }

    private async ValueTask NotifyAsync(Player player, string code, string en, string vi)
    {
        var isVi = string.Equals(player.Culture.TwoLetterISOLanguageName, "vi", StringComparison.OrdinalIgnoreCase);
        await player.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(isVi ? vi : en, MessageType.BlueNormal)).ConfigureAwait(false);
        await this.SendStateAsync(player, requested: true, actionResult: code, messageEn: en, messageVi: vi).ConfigureAwait(false);
    }
}
