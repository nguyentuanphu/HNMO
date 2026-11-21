// <copyright file="MountChainPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.MountChain;

using System.Runtime.InteropServices;
using HNMO.AttributeSystem;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlugIns.AccountSafety;
using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.Inventory;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.PlugIns;

/// <summary>
/// Progressive Mount Chain plug-in (OFF by default, hotkey <kbd>Shift+U</kbd>):
/// Golden Boss kills grant Mount Shards; duplicate bag mounts craft next tiers or salvage into shards;
/// unlocked collection tiers grant modest PvE-only bonuses and display on the website collection widget.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Mount Chain", Description = "Golden Boss shard drops, duplicate-mount upgrades, salvage, and PvE collection aura (OFF by default).")]
[Guid("B93F5E71-2C64-48D3-8E1A-4D7F6B209145")]
public sealed class MountChainPlugIn :
    IPlayerStateChangedPlugIn,
    IAttackableGotKilledPlugIn,
    ISupportCustomConfiguration<MountChainConfiguration>,
    ISupportDefaultCustomConfiguration,
    IDisabledByDefault
{
    /// <summary>Effect number outside desktop visual effect range.</summary>
    public const short BuffEffectNumber = 242;

    private static readonly TimeSpan BuffDuration = TimeSpan.FromMinutes(30);
    private static readonly MagicEffectDefinition BuffDefinition = new()
    {
        Number = BuffEffectNumber,
        Name = "HNMO mount collection",
        SubType = 242,
        InformObservers = false,
        StopByDeath = false,
        SendDuration = false,
    };

    private (MountChainConfiguration Config, MountChainEngine Engine)? _engine;

    /// <inheritdoc />
    public MountChainConfiguration? Configuration { get; set; }

    /// <summary>Gets the active <see cref="MountChainPlugIn"/> for <paramref name="gameContext"/>, or <c>null</c>.</summary>
    /// <param name="gameContext">Game context.</param>
    /// <returns>Active plug-in or <c>null</c>.</returns>
    public static MountChainPlugIn? Get(IGameContext gameContext) =>
        gameContext.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<MountChainPlugIn>().FirstOrDefault();

    /// <inheritdoc />
    public object CreateDefaultConfig() => new MountChainConfiguration();

    /// <summary>Gets or creates the engine.</summary>
    /// <returns>Engine.</returns>
    public MountChainEngine GetEngine()
    {
        var cfg = this.Configuration ?? new MountChainConfiguration();
        if (this._engine is { } cached && ReferenceEquals(cached.Config, cfg))
        {
            return cached.Engine;
        }

        var engine = new MountChainEngine(cfg, MountChainStore.Open(cfg.FileName), enabled: this.Configuration is not null);
        this._engine = (cfg, engine);
        return engine;
    }

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (this.Configuration is null || currentState != PlayerState.EnteredWorld)
        {
            return;
        }

        await this.TryDeliverOwedAsync(player).ConfigureAwait(false);
        await this.ApplyActiveBuffAsync(player).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask AttackableGotKilledAsync(IAttackable killed, IAttacker? killer)
    {
        if (this.Configuration is null
            || killed is not Monster monster
            || monster.Definition is not { } def
            || killer is not Player player
            || player.Account is not { } account)
        {
            return;
        }

        var engine = this.GetEngine();
        if (!engine.Enabled || !engine.IsGoldenBoss(def.Number, def.Designation))
        {
            return;
        }

        if (engine.Configuration.GoldenDropChancePercent < 100
            && Random.Shared.Next(100) >= engine.Configuration.GoldenDropChancePercent)
        {
            return;
        }

        var shards = Math.Max(1, engine.Configuration.ShardsPerGoldenBoss);
        var total = engine.AwardShards(account.LoginName, shards);
        await player.ShowBlueMessageAsync(IsVietnamese(player)
            ? $"Boss Hoàng Kim rơi +{shards} Mảnh Thú Cưỡi (Tổng: {total}). Nhấn Shift+U để nâng cấp."
            : $"Golden Boss dropped +{shards} Mount Shards (Total: {total}). Press Shift+U to upgrade mounts.").ConfigureAwait(false);
        await this.SendStateAsync(player, requested: false).ConfigureAwait(false);
    }

    /// <summary>Sends the player's current Mount Chain collection state.</summary>
    /// <param name="player">Player.</param>
    /// <param name="requested">Whether requested by UI.</param>
    /// <param name="notice">Optional feedback notice.</param>
    /// <returns>A task representing the send.</returns>
    public async ValueTask SendStateAsync(
        Player player,
        bool requested,
        (string Status, string MessageEn, string MessageVi)? notice = null)
    {
        var engine = this.GetEngine();
        var bag = this.ReadPlayerBag(player);
        var json = MountChainStateWriter.WriteForPlayer(engine, player, bag, notice);
        await player.InvokeViewPlugInAsync<IHnmoMountChainViewPlugIn>(p => p.ShowMountChainAsync(json, requested)).ConfigureAwait(false);
    }

    /// <summary>Crafts/upgrades into <paramref name="tierId"/> consuming shards, Zen, and previous-tier bag mounts.</summary>
    /// <param name="player">Player.</param>
    /// <param name="tierId">Target tier ID.</param>
    /// <returns>A task.</returns>
    public async ValueTask CraftTierAsync(Player player, string tierId)
    {
        if (player.SelectedCharacter is null || player.Account is null || player.Inventory is null)
        {
            return;
        }

        var login = player.Account.LoginName;
        var engine = this.GetEngine();
        var bag = this.ReadPlayerBag(player);
        var check = engine.PlanCraft(login, tierId, bag, player.Money, out var consumedSlots);
        if (check != MountActionResult.Ok)
        {
            var (en, vi) = DescribeFailure(check);
            await player.ShowBlueMessageAsync(IsVietnamese(player) ? vi : en).ConfigureAwait(false);
            await this.SendStateAsync(player, requested: true, ("error", en, vi)).ConfigureAwait(false);
            return;
        }

        var tier = engine.Configuration.Tiers.First(t => string.Equals(t.Id, tierId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (tier.ZenCost > 0)
        {
            if (!player.TryRemoveMoney(tier.ZenCost))
            {
                var (en, vi) = DescribeFailure(MountActionResult.NotEnoughZen);
                await player.ShowBlueMessageAsync(IsVietnamese(player) ? vi : en).ConfigureAwait(false);
                await this.SendStateAsync(player, requested: true, ("error", en, vi)).ConfigureAwait(false);
                return;
            }

            await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
        }

        await RemoveBagSlotsAsync(player, consumedSlots).ConfigureAwait(false);
        engine.CompleteCraft(login, tier, DateTime.UtcNow);

        var reward = new GiftCodeOwedReward
        {
            Reference = $"mount-craft:{tier.Id}:{Guid.NewGuid():N}",
            CodeId = tier.Id,
            Code = tier.Id,
            Zen = 0,
            Items = [new GiftCodeItem { Group = tier.ItemGroup, Number = tier.ItemNumber, Level = tier.ItemLevel }],
        };

        var delivered = await GiftCodeRewards.GiveGoodsAsync(player, reward).ConfigureAwait(false);
        if (!delivered)
        {
            var charId = player.SelectedCharacter.GetId().ToString();
            engine.Store.Update(d =>
            {
                if (!d.Owed.TryGetValue(charId, out var list))
                {
                    list = [];
                    d.Owed[charId] = list;
                }

                list.Add(reward);
            });
        }

        await this.ApplyActiveBuffAsync(player).ConfigureAwait(false);

        var msgEn = delivered
            ? $"Unlocked & forged {tier.NameEn}!"
            : $"Unlocked {tier.NameEn}! Bag full — the mount item will be delivered when space is free.";
        var msgVi = delivered
            ? $"Đã mở khóa & chế tạo {tier.NameVi}!"
            : $"Đã mở khóa {tier.NameVi}! Túi đồ đầy — vật phẩm sẽ tự động giao khi còn chỗ.";

        await player.ShowBlueMessageAsync(IsVietnamese(player) ? msgVi : msgEn).ConfigureAwait(false);
        await this.SendStateAsync(player, requested: true, ("ok", msgEn, msgVi)).ConfigureAwait(false);
    }

    /// <summary>Activates an already-unlocked collection tier for its modest PvE bonuses.</summary>
    /// <param name="player">Player.</param>
    /// <param name="tierId">Tier ID.</param>
    /// <returns>A task.</returns>
    public async ValueTask ActivateTierAsync(Player player, string tierId)
    {
        if (player.Account is null)
        {
            return;
        }

        var engine = this.GetEngine();
        var res = engine.ActivateTier(player.Account.LoginName, tierId);
        if (res != MountActionResult.Ok)
        {
            var (en, vi) = DescribeFailure(res);
            await player.ShowBlueMessageAsync(IsVietnamese(player) ? vi : en).ConfigureAwait(false);
            await this.SendStateAsync(player, requested: true, ("error", en, vi)).ConfigureAwait(false);
            return;
        }

        await this.ApplyActiveBuffAsync(player).ConfigureAwait(false);
        var tier = engine.GetActiveTier(player.Account.LoginName);
        var msgEn = $"Active mount aura switched to {tier?.NameEn ?? tierId}.";
        var msgVi = $"Đã kích hoạt hiệu ứng thú cưỡi: {tier?.NameVi ?? tierId}.";
        await player.ShowBlueMessageAsync(IsVietnamese(player) ? msgVi : msgEn).ConfigureAwait(false);
        await this.SendStateAsync(player, requested: true, ("ok", msgEn, msgVi)).ConfigureAwait(false);
    }

    /// <summary>Salvages one unlocked duplicate bag mount of <paramref name="tierId"/> into Mount Shards.</summary>
    /// <param name="player">Player.</param>
    /// <param name="tierId">Tier ID.</param>
    /// <returns>A task.</returns>
    public async ValueTask SalvageDuplicateAsync(Player player, string tierId)
    {
        if (player.Account is null)
        {
            return;
        }

        var engine = this.GetEngine();
        var bag = this.ReadPlayerBag(player);
        var check = engine.PlanSalvage(tierId, bag, out var slot);
        if (check != MountActionResult.Ok)
        {
            var (en, vi) = DescribeFailure(check);
            await player.ShowBlueMessageAsync(IsVietnamese(player) ? vi : en).ConfigureAwait(false);
            await this.SendStateAsync(player, requested: true, ("error", en, vi)).ConfigureAwait(false);
            return;
        }

        var tier = engine.Configuration.Tiers.First(t => string.Equals(t.Id, tierId.Trim(), StringComparison.OrdinalIgnoreCase));
        await RemoveBagSlotsAsync(player, [slot]).ConfigureAwait(false);
        var total = engine.AwardShards(player.Account.LoginName, tier.ShardsOnSalvage);
        var msgEn = $"Salvaged duplicate {tier.NameEn} for +{tier.ShardsOnSalvage} Shards (Total: {total}).";
        var msgVi = $"Đã tách {tier.NameVi} trùng nhận +{tier.ShardsOnSalvage} Mảnh (Tổng: {total}).";
        await player.ShowBlueMessageAsync(IsVietnamese(player) ? msgVi : msgEn).ConfigureAwait(false);
        await this.SendStateAsync(player, requested: true, ("ok", msgEn, msgVi)).ConfigureAwait(false);
    }

    private static (string En, string Vi) DescribeFailure(MountActionResult result) => result switch
    {
        MountActionResult.Disabled => ("Mount Chain is currently disabled.", "Chuỗi thú cưỡi đang tắt."),
        MountActionResult.NotFound => ("Mount tier not found.", "Không tìm thấy bậc thú cưỡi."),
        MountActionResult.NotEnoughShards => ("Not enough Mount Shards (hunt Golden Bosses or salvage duplicates).", "Không đủ Mảnh Thú Cưỡi (săn Boss Vàng hoặc tách thú trùng)."),
        MountActionResult.NotEnoughZen => ("Not enough Zen.", "Không đủ Zen."),
        MountActionResult.MissingPreviousMount => ("Missing required previous-tier unlocked mount in your bag.", "Thiếu thú cưỡi bậc trước (chưa khóa) trong túi đồ."),
        MountActionResult.MissingDuplicateToSalvage => ("No unlocked mount of that tier in your bag to salvage.", "Không có thú cưỡi chưa khóa thuộc bậc đó trong túi để tách."),
        MountActionResult.NotUnlocked => ("Mount tier not unlocked on your account yet.", "Tài khoản chưa mở khóa bậc thú cưỡi này."),
        _ => ("Mount action failed.", "Không thể thực hiện."),
    };

    private static async ValueTask RemoveBagSlotsAsync(Player player, IReadOnlyList<byte> slots)
    {
        if (player.Inventory is not { } inv)
        {
            return;
        }

        foreach (var slot in slots)
        {
            var item = inv.GetItem(slot);
            if (item is null)
            {
                continue;
            }

            await inv.RemoveItemAsync(item).ConfigureAwait(false);
            await player.PersistenceContext.DeleteAsync(item).ConfigureAwait(false);
            await player.InvokeViewPlugInAsync<IItemRemovedPlugIn>(p => p.RemoveItemAsync(slot)).ConfigureAwait(false);
        }
    }

    private static bool IsVietnamese(Player player) =>
        player.Culture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);

    private List<MountBagItem> ReadPlayerBag(Player player)
    {
        var safety = AccountSafetyPlugIn.Get(player.GameContext)?.GetEngine();
        var login = player.Account?.LoginName;
        Func<Item, bool>? isLocked = safety is not null && !string.IsNullOrEmpty(login)
            ? item => safety.IsLocked(login, item.GetId(), DateTime.UtcNow)
            : null;
        return MountChainStateWriter.ReadBag(player, isLocked);
    }

    private async ValueTask ApplyActiveBuffAsync(Player player)
    {
        if (player.Account is not { } acc)
        {
            return;
        }

        var tier = this.GetEngine().GetActiveTier(acc.LoginName);
        player.MagicEffectList.TryGetEffect(BuffEffectNumber, out var existing);
        if (existing is not null)
        {
            await existing.DisposeAsync().ConfigureAwait(false);
        }

        if (tier is null)
        {
            return;
        }

        var elements = new List<MagicEffect.ElementWithTarget>();
        if (tier.BonusExpPercent > 0)
        {
            elements.Add(new MagicEffect.ElementWithTarget(new ConstantElement((float)(tier.BonusExpPercent / 100.0), AggregateType.AddRaw), Stats.BonusExperienceRate));
        }

        if (tier.BonusZenPercent > 0)
        {
            elements.Add(new MagicEffect.ElementWithTarget(new ConstantElement((float)(1 + (tier.BonusZenPercent / 100.0)), AggregateType.Multiplicate), Stats.MoneyAmountRate));
        }

        if (tier.PveDefenseBonus > 0)
        {
            elements.Add(new MagicEffect.ElementWithTarget(new ConstantElement(tier.PveDefenseBonus, AggregateType.AddFinal), Stats.DefenseFinal));
        }

        if (elements.Count > 0)
        {
            await player.MagicEffectList.AddEffectAsync(new MagicEffect(BuffDuration, BuffDefinition, elements.ToArray())).ConfigureAwait(false);
        }
    }

    private async ValueTask TryDeliverOwedAsync(Player player)
    {
        if (player.SelectedCharacter is null)
        {
            return;
        }

        var charId = player.SelectedCharacter.GetId().ToString();
        var engine = this.GetEngine();
        List<GiftCodeOwedReward> owed = [];
        engine.Store.Update(d =>
        {
            if (d.Owed.Remove(charId, out var list))
            {
                owed = list;
            }
        });

        if (owed.Count == 0)
        {
            return;
        }

        var remaining = new List<GiftCodeOwedReward>();
        foreach (var reward in owed)
        {
            if (!await GiftCodeRewards.GiveGoodsAsync(player, reward).ConfigureAwait(false))
            {
                remaining.Add(reward);
            }
        }

        if (remaining.Count > 0)
        {
            engine.Store.Update(d =>
            {
                d.Owed[charId] = remaining;
            });
        }
    }
}
