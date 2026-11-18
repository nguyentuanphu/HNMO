// <copyright file="CombiningLeftoversPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.CombiningLeftovers;

using System.Runtime.InteropServices;
using HNMO.GameLogic.CashShop;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlugIns.AccountSafety;
using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.Inventory;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.PlugIns;

/// <summary>
/// Combining Leftovers &amp; Surplus Material Exchange plug-in (OFF by default, hotkey <kbd>F10</kbd> or Exchange NPC):
/// lets players upgrade leftover Kundun boxes using jewels + Zen, and trade surplus boss/event items for coins/Zen/items.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Combining Leftovers", Description = "Upgrade leftover boxes with jewels and exchange surplus event materials (OFF by default).")]
[Guid("D62C91E4-4A18-4D72-8F1B-3C5E9A204711")]
public sealed class CombiningLeftoversPlugIn :
    IPlayerStateChangedPlugIn,
    IPlayerTalkToNpcPlugIn,
    ISupportCustomConfiguration<CombiningLeftoversConfiguration>,
    ISupportDefaultCustomConfiguration,
    IDisabledByDefault
{
    private (CombiningLeftoversConfiguration Config, CombiningLeftoversEngine Engine)? _engine;

    /// <inheritdoc />
    public CombiningLeftoversConfiguration? Configuration { get; set; }

    /// <summary>Gets the active <see cref="CombiningLeftoversPlugIn"/> for a game context, or <c>null</c>.</summary>
    /// <param name="gameContext">Game context.</param>
    /// <returns>Active plug-in or <c>null</c>.</returns>
    public static CombiningLeftoversPlugIn? Get(IGameContext gameContext) =>
        gameContext.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<CombiningLeftoversPlugIn>().FirstOrDefault();

    /// <inheritdoc />
    public object CreateDefaultConfig() => new CombiningLeftoversConfiguration();

    /// <summary>Gets or creates the engine.</summary>
    /// <returns>Leftovers engine.</returns>
    public CombiningLeftoversEngine GetEngine()
    {
        var cfg = this.Configuration ??= new CombiningLeftoversConfiguration();
        if (this._engine is { } cached && ReferenceEquals(cached.Config, cfg))
        {
            return cached.Engine;
        }

        var engine = new CombiningLeftoversEngine(cfg, CombiningLeftoversStore.Open(cfg.FileName), enabled: true);
        this._engine = (cfg, engine);
        return engine;
    }

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (currentState == PlayerState.EnteredWorld)
        {
            await this.TryDeliverOwedAsync(player).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask PlayerTalksToNpcAsync(Player player, NonPlayerCharacter npc, NpcTalkEventArgs eventArgs)
    {
        var cfg = this.Configuration ??= new CombiningLeftoversConfiguration();
        if (cfg.ExchangeNpcNumber <= 0 || npc.Definition.Number != cfg.ExchangeNpcNumber)
        {
            return;
        }

        eventArgs.HasBeenHandled = true;
        eventArgs.LeavesDialogOpen = false;
        await this.SendStateAsync(player, requested: true).ConfigureAwait(false);
        await player.ShowBlueMessageAsync(IsVietnamese(player)
            ? "Mở bảng Đổi đồ dư & Nâng cấp Hộp (F10)."
            : "Opened Combining Leftovers & Surplus Exchange (F10).").ConfigureAwait(false);
    }

    /// <summary>Sends the player's current recipes, offers, bag item counts, and optional action notice.</summary>
    /// <param name="player">Player.</param>
    /// <param name="requested">Whether requested by client.</param>
    /// <param name="notice">Optional feedback notice.</param>
    /// <returns>A task representing the send.</returns>
    public async ValueTask SendStateAsync(
        Player player,
        bool requested,
        (string Status, string MessageEn, string MessageVi)? notice = null)
    {
        var engine = this.GetEngine();
        var bag = this.ReadPlayerBag(player);
        var json = CombiningLeftoversStateWriter.WriteForPlayer(engine, player, bag, seasonCapOverride: null, notice);
        await player.InvokeViewPlugInAsync<IHnmoLeftoversViewPlugIn>(p => p.ShowLeftoversAsync(json, requested)).ConfigureAwait(false);
    }

    /// <summary>Executes a leftover upgrade recipe for <paramref name="player"/>.</summary>
    /// <param name="player">Player.</param>
    /// <param name="recipeId">Recipe ID.</param>
    /// <returns>A task representing the combine.</returns>
    public async ValueTask CombineAsync(Player player, string recipeId)
    {
        if (player.SelectedCharacter is null || player.Account is null || player.Inventory is null)
        {
            return;
        }

        var engine = this.GetEngine();
        var bag = this.ReadPlayerBag(player);
        var check = engine.PlanCombine(recipeId, bag, player.Money, seasonCapOverride: null, out var pickedSlots);
        if (check != LeftoversCheckResult.Ok)
        {
            var (en, vi) = DescribeFailure(check);
            await player.ShowBlueMessageAsync(IsVietnamese(player) ? vi : en).ConfigureAwait(false);
            await this.SendStateAsync(player, requested: true, ("error", en, vi)).ConfigureAwait(false);
            return;
        }

        var recipe = engine.Configuration.Recipes.First(r => r.Enabled && string.Equals(r.Id, recipeId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (recipe.ZenCost > 0)
        {
            if (!player.TryRemoveMoney(recipe.ZenCost))
            {
                var (en, vi) = DescribeFailure(LeftoversCheckResult.NotEnoughZen);
                await player.ShowBlueMessageAsync(IsVietnamese(player) ? vi : en).ConfigureAwait(false);
                await this.SendStateAsync(player, requested: true, ("error", en, vi)).ConfigureAwait(false);
                return;
            }

            await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
        }

        await RemoveBagSlotsAsync(player, pickedSlots).ConfigureAwait(false);

        var success = recipe.SuccessRatePercent >= 100 || Random.Shared.Next(100) < recipe.SuccessRatePercent;
        string msgEn;
        string msgVi;
        string status;
        if (success)
        {
            var reward = new GiftCodeOwedReward
            {
                Reference = $"leftover-cb:{recipe.Id}:{Guid.NewGuid():N}",
                CodeId = recipe.Id,
                Code = recipe.Id,
                Zen = 0,
                Items = [new GiftCodeItem { Group = recipe.OutputGroup, Number = recipe.OutputNumber, Level = recipe.OutputLevel }],
            };

            var delivered = await GiftCodeRewards.GiveGoodsAsync(player, reward).ConfigureAwait(false);
            if (!delivered)
            {
                engine.Store.AddOwed(player.SelectedCharacter.GetId().ToString(), reward);
            }

            engine.Store.AppendLog(new LeftoversLogEntry
            {
                AtUtc = DateTime.UtcNow,
                LoginName = player.Account.LoginName,
                Action = "Combine",
                TargetId = recipe.Id,
                Detail = delivered ? recipe.NameEn : $"{recipe.NameEn} (owed)",
            });

            status = "ok";
            msgEn = delivered
                ? $"Combined: {recipe.NameEn}."
                : $"Combined: {recipe.NameEn}. Your bag is full — the output item was saved and will be delivered when space is free.";
            msgVi = delivered
                ? $"Ghép thành công: {recipe.NameVi}."
                : $"Ghép thành công: {recipe.NameVi}. Túi đồ đầy — vật phẩm sẽ tự động giao khi túi còn chỗ.";
        }
        else
        {
            engine.Store.AppendLog(new LeftoversLogEntry
            {
                AtUtc = DateTime.UtcNow,
                LoginName = player.Account.LoginName,
                Action = "CombineFail",
                TargetId = recipe.Id,
                Detail = $"{recipe.NameEn} ({recipe.SuccessRatePercent}%)",
            });

            status = "fail";
            msgEn = $"Combine failed ({recipe.SuccessRatePercent}% chance): {recipe.NameEn}. Materials were consumed.";
            msgVi = $"Ghép thất bại (tỉ lệ {recipe.SuccessRatePercent}%): {recipe.NameVi}. Nguyên liệu đã bị tiêu hao.";
        }

        await player.ShowBlueMessageAsync(IsVietnamese(player) ? msgVi : msgEn).ConfigureAwait(false);
        await this.SendStateAsync(player, requested: true, (status, msgEn, msgVi)).ConfigureAwait(false);
        await this.TryDeliverOwedAsync(player).ConfigureAwait(false);
    }

    /// <summary>Executes a surplus material exchange offer for <paramref name="player"/>.</summary>
    /// <param name="player">Player.</param>
    /// <param name="offerId">Offer ID.</param>
    /// <returns>A task representing the exchange.</returns>
    public async ValueTask ExchangeAsync(Player player, string offerId)
    {
        if (player.SelectedCharacter is null || player.Account is null || player.Inventory is null)
        {
            return;
        }

        var engine = this.GetEngine();
        var bag = this.ReadPlayerBag(player);
        var check = engine.PlanExchange(offerId, bag, out var pickedSlots);
        if (check != LeftoversCheckResult.Ok)
        {
            var (en, vi) = DescribeFailure(check);
            await player.ShowBlueMessageAsync(IsVietnamese(player) ? vi : en).ConfigureAwait(false);
            await this.SendStateAsync(player, requested: true, ("error", en, vi)).ConfigureAwait(false);
            return;
        }

        var offer = engine.Configuration.ExchangeOffers.First(o => o.Enabled && string.Equals(o.Id, offerId.Trim(), StringComparison.OrdinalIgnoreCase));
        await RemoveBagSlotsAsync(player, pickedSlots).ConfigureAwait(false);

        var opId = Guid.NewGuid().ToString("N");
        var login = player.Account.LoginName;
        if (offer.RewardCoins > 0)
        {
            await MissionCoinGranter.GrantAsync(
                player.GameContext,
                new CoinGrant(login, offer.RewardCoinType, offer.RewardCoins, $"Surplus exchange: {offer.NameEn}", $"leftover-ex:{login}:{offer.Id}:{opId}"),
                "HNMO leftovers").ConfigureAwait(false);
            await player.ApplyPendingCashShopCoinGrantsAsync().ConfigureAwait(false);
        }

        var deliveredAll = true;
        if (offer.RewardZen > 0 || offer.RewardItemCount > 0)
        {
            var items = new List<GiftCodeItem>();
            for (var i = 0; i < offer.RewardItemCount; i++)
            {
                items.Add(new GiftCodeItem
                {
                    Group = offer.RewardGroup,
                    Number = offer.RewardNumber,
                    Level = offer.RewardLevel,
                });
            }

            var reward = new GiftCodeOwedReward
            {
                Reference = $"leftover-ex-item:{offer.Id}:{opId}",
                CodeId = offer.Id,
                Code = offer.Id,
                Zen = offer.RewardZen,
                Items = items,
            };

            deliveredAll = await GiftCodeRewards.GiveGoodsAsync(player, reward).ConfigureAwait(false);
            if (!deliveredAll)
            {
                engine.Store.AddOwed(player.SelectedCharacter.GetId().ToString(), reward);
            }
        }

        engine.Store.AppendLog(new LeftoversLogEntry
        {
            AtUtc = DateTime.UtcNow,
            LoginName = login,
            Action = "Exchange",
            TargetId = offer.Id,
            Detail = offer.NameEn,
        });

        var msgEn = deliveredAll
            ? $"Exchanged: {offer.NameEn}."
            : $"Exchanged: {offer.NameEn}. Bag is full — item/Zen reward saved for delivery when space is free.";
        var msgVi = deliveredAll
            ? $"Đổi thành công: {offer.NameVi}."
            : $"Đổi thành công: {offer.NameVi}. Túi đồ đầy — phần thưởng sẽ tự động giao khi túi còn chỗ.";

        await player.ShowBlueMessageAsync(IsVietnamese(player) ? msgVi : msgEn).ConfigureAwait(false);
        await this.SendStateAsync(player, requested: true, ("ok", msgEn, msgVi)).ConfigureAwait(false);
    }

    private static (string En, string Vi) DescribeFailure(LeftoversCheckResult result) => result switch
    {
        LeftoversCheckResult.Disabled => ("Combining leftovers is currently disabled.", "Tính năng Đổi đồ dư đang tắt."),
        LeftoversCheckResult.NotFound => ("Recipe or exchange offer not found.", "Không tìm thấy công thức hoặc gói đổi."),
        LeftoversCheckResult.BlockedBySeasonStage => ("Blocked by the current Season Plan box level cap.", "Đang bị giới hạn bởi mốc cấp Hộp của Lộ trình mùa."),
        LeftoversCheckResult.NotEnoughZen => ("Not enough Zen.", "Không đủ Zen."),
        LeftoversCheckResult.MissingIngredients => ("Missing required unlocked items in your bag.", "Thiếu vật phẩm chưa khóa trong túi đồ."),
        _ => ("Action could not be completed.", "Không thể thực hiện."),
    };

    private static async ValueTask RemoveBagSlotsAsync(Player player, IReadOnlyList<byte> slots)
    {
        if (player.Inventory is not { } inventory)
        {
            return;
        }

        foreach (var slot in slots)
        {
            var item = inventory.GetItem(slot);
            if (item is null)
            {
                continue;
            }

            await inventory.RemoveItemAsync(item).ConfigureAwait(false);
            await player.InvokeViewPlugInAsync<IItemRemovedPlugIn>(p => p.RemoveItemAsync(slot)).ConfigureAwait(false);
        }
    }

    private static bool IsVietnamese(Player player) =>
        player.Culture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);

    private List<BagItemDescriptor> ReadPlayerBag(Player player)
    {
        var safety = AccountSafetyPlugIn.Get(player.GameContext)?.GetEngine();
        var login = player.Account?.LoginName;
        return CombiningLeftoversStateWriter.ReadBag(
            player,
            safety is not null && !string.IsNullOrEmpty(login)
                ? item => safety.IsLocked(login, item.GetId(), DateTime.UtcNow)
                : null);
    }

    private async ValueTask TryDeliverOwedAsync(Player player)
    {
        if (player.SelectedCharacter is null)
        {
            return;
        }

        var charId = player.SelectedCharacter.GetId().ToString();
        var engine = this.GetEngine();
        var owed = engine.Store.TakeOwed(charId);
        if (owed.Count == 0)
        {
            return;
        }

        var remaining = new List<GiftCodeOwedReward>();
        var deliveredCount = 0;
        foreach (var reward in owed)
        {
            if (await GiftCodeRewards.GiveGoodsAsync(player, reward).ConfigureAwait(false))
            {
                deliveredCount++;
            }
            else
            {
                remaining.Add(reward);
            }
        }

        engine.Store.ReturnOwed(charId, remaining);
        if (deliveredCount > 0)
        {
            await player.ShowBlueMessageAsync(IsVietnamese(player)
                ? $"Đã giao {deliveredCount} phần thưởng Đổi đồ dư còn thiếu từ túi đồ trước đó."
                : $"Delivered {deliveredCount} pending Combining Leftovers reward(s) to your bag.").ConfigureAwait(false);
        }
    }
}
