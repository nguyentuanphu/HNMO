// <copyright file="TalismanPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Talismans;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.Views.Character;
using HNMO.Persistence;
using HNMO.PlugIns;

/// <summary>
/// Admin-defined Talismans/Figurines (passive bag bonuses &amp; buffs) and capped Stat-Point Consumables (OFF by default).
/// </summary>
[PlugIn]
[Display(Name = "HNMO Talismans & Stat Consumables", Description = "Grants passive bonuses or buffs while carrying talismans/figurines in the bag, and supports stat-point consumables up to a configured cap (OFF by default).")]
[Guid("D62E4F18-3A9C-4E75-B842-9A1D7E3F5C02")]
public sealed class TalismanPlugIn :
    IPlayerStateChangedPlugIn,
    ISupportCustomConfiguration<TalismanConfiguration>,
    ISupportDefaultCustomConfiguration,
    IDisabledByDefault
{
    /// <summary>Passive magic effect number reserved for carried bag talismans.</summary>
    public const short BuffEffectNumber = 243;

    private static readonly TimeSpan BuffDuration = TimeSpan.FromDays(7);
    private static readonly MagicEffectDefinition BagBuffDefinition = new()
    {
        Number = BuffEffectNumber,
        Name = "HNMO Talisman Passive",
        SubType = 243,
        InformObservers = false,
        StopByDeath = false,
        SendDuration = false,
    };

    private readonly ConditionalWeakTable<Player, HashSet<short>> _grantedEffectsByPlayer = new();
    private TalismanStore? _store;

    /// <inheritdoc />
    public TalismanConfiguration? Configuration { get; set; }

    /// <summary>
    /// Gets the active <see cref="TalismanPlugIn"/> from <paramref name="gameContext"/>, or <c>null</c> when disabled.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The active plug-in, or <c>null</c>.</returns>
    public static TalismanPlugIn? Get(IGameContext gameContext) =>
        gameContext.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<TalismanPlugIn>().FirstOrDefault();

    /// <summary>
    /// Recomputes and applies carried bag talisman bonuses and buffs for <paramref name="player"/> when active.
    /// </summary>
    /// <param name="player">The player whose inventory changed.</param>
    public static ValueTask RefreshAsync(Player player)
    {
        if (player.PlayerState.CurrentState != PlayerState.EnteredWorld)
        {
            return ValueTask.CompletedTask;
        }

        return Get(player.GameContext) is { } plugIn
            ? plugIn.ApplyBagBonusesAsync(player)
            : ValueTask.CompletedTask;
    }

    /// <summary>
    /// Attempts to consume <paramref name="item"/> as an admin-configured stat-point consumable.
    /// </summary>
    /// <param name="player">The player consuming the item.</param>
    /// <param name="item">The consumed inventory item.</param>
    /// <returns>A tuple (<c>Handled</c>, <c>Succeeded</c>).</returns>
    public static async ValueTask<(bool Handled, bool Succeeded)> TryConsumeStatItemAsync(Player player, Item item)
    {
        if (Get(player.GameContext) is not { } plugIn)
        {
            return (false, false);
        }

        return await plugIn.ConsumeStatItemInternalAsync(player, item).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new TalismanConfiguration();

    /// <inheritdoc />
    public ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (currentState != PlayerState.EnteredWorld || this.Configuration is null)
        {
            return ValueTask.CompletedTask;
        }

        return this.ApplyBagBonusesAsync(player);
    }

    /// <summary>Re-evaluates carried bag talismans and updates the player's passive effects.</summary>
    /// <param name="player">Target player.</param>
    public async ValueTask ApplyBagBonusesAsync(Player player)
    {
        if (player.Inventory is null || player.SelectedCharacter is null)
        {
            return;
        }

        var cfg = this.Configuration ?? new TalismanConfiguration();
        var summary = TalismanEngine.EvaluateBagBonuses(player.Inventory.Items, cfg);

        if (player.MagicEffectList.TryGetEffect(BuffEffectNumber, out var existing) && existing is not null)
        {
            await existing.DisposeAsync().ConfigureAwait(false);
        }

        var tracked = this._grantedEffectsByPlayer.GetValue(player, _ => []);
        foreach (var prevEffect in tracked.ToList())
        {
            if (!summary.GrantedEffectNumbers.Contains(prevEffect)
                && player.MagicEffectList.TryGetEffect(prevEffect, out var prevBuff)
                && prevBuff is not null)
            {
                await prevBuff.DisposeAsync().ConfigureAwait(false);
                tracked.Remove(prevEffect);
            }
        }

        if (summary.HasAnyStatBonus)
        {
            var elements = new List<MagicEffect.ElementWithTarget>();
            if (summary.BonusExpPercent > 0)
            {
                elements.Add(new MagicEffect.ElementWithTarget(
                    new ConstantElement(summary.BonusExpPercent / 100f, AggregateType.AddRaw),
                    Stats.BonusExperienceRate));
            }

            if (summary.BonusZenPercent > 0)
            {
                elements.Add(new MagicEffect.ElementWithTarget(
                    new ConstantElement(1f + (summary.BonusZenPercent / 100f), AggregateType.Multiplicate),
                    Stats.MoneyAmountRate));
            }

            if (summary.BonusDamage > 0)
            {
                elements.Add(new MagicEffect.ElementWithTarget(
                    new ConstantElement(summary.BonusDamage, AggregateType.AddFinal),
                    Stats.GreaterDamageBonus));
            }

            if (summary.BonusDefense > 0)
            {
                elements.Add(new MagicEffect.ElementWithTarget(
                    new ConstantElement(summary.BonusDefense, AggregateType.AddFinal),
                    Stats.DefenseFinal));
            }

            if (summary.BonusMaxHp > 0)
            {
                elements.Add(new MagicEffect.ElementWithTarget(
                    new ConstantElement(summary.BonusMaxHp, AggregateType.AddFinal),
                    Stats.MaximumHealth));
            }

            if (summary.BonusAttackSpeed > 0)
            {
                elements.Add(new MagicEffect.ElementWithTarget(
                    new ConstantElement(summary.BonusAttackSpeed, AggregateType.AddRaw),
                    Stats.AttackSpeedAny));
            }

            if (summary.CriticalChancePercent > 0)
            {
                elements.Add(new MagicEffect.ElementWithTarget(
                    new ConstantElement(summary.CriticalChancePercent / 100f, AggregateType.AddRaw),
                    Stats.CriticalDamageChance));
            }

            if (elements.Count > 0)
            {
                await player.MagicEffectList.AddEffectAsync(
                    new MagicEffect(BuffDuration, BagBuffDefinition, [.. elements])).ConfigureAwait(false);
            }
        }

        if (summary.GrantedEffectNumbers.Count > 0 && player.Attributes is not null)
        {
            var byNumber = player.GameContext.Configuration.MagicEffects
                .GroupBy(e => e.Number)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var effNumber in summary.GrantedEffectNumbers)
            {
                if (!byNumber.TryGetValue(effNumber, out var def))
                {
                    continue;
                }

                if (player.MagicEffectList.ContainsEffect(effNumber))
                {
                    tracked.Add(effNumber);
                    continue;
                }

                var boosts = def.PowerUpDefinitions
                    .Where(d => d.Boost is not null && d.TargetAttribute is not null)
                    .Select(d => new MagicEffect.ElementWithTarget(player.Attributes.CreateElement(d), d.TargetAttribute!))
                    .ToArray();
                if (boosts.Length == 0)
                {
                    continue;
                }

                await player.MagicEffectList.AddEffectAsync(new MagicEffect(BuffDuration, def, boosts)).ConfigureAwait(false);
                tracked.Add(effNumber);
            }
        }
    }

    private TalismanStore GetStore()
    {
        if (this._store is not null)
        {
            return this._store;
        }

        var cfg = this.Configuration ?? new TalismanConfiguration();
        this._store = TalismanStore.Open(cfg.FileName);
        return this._store;
    }

    private async ValueTask<(bool Handled, bool Succeeded)> ConsumeStatItemInternalAsync(Player player, Item item)
    {
        if (player.SelectedCharacter is not { } character || player.Attributes is null)
        {
            return (false, false);
        }

        var cfg = this.Configuration ?? new TalismanConfiguration();
        var store = this.GetStore();
        var charKey = character.GetId().ToString();
        var claimed = store.GetTotalClaimed(charKey);
        var allowsCommand = character.CharacterClass?.GetStatAttribute(Stats.BaseLeadership)?.IncreasableByPlayer == true;

        var decision = TalismanEngine.EvaluateStatConsume(cfg, item, claimed, allowsCommand);
        if (!decision.IsConfiguredConsumable)
        {
            return (false, false);
        }

        if (player.PlayerState.CurrentState != PlayerState.EnteredWorld)
        {
            // The same state check as every other item use (BaseConsumeHandlerPlugIn): not while dead, trading
            // or in an NPC dialog.
            return (true, false);
        }

        var isVi = player.Culture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
        if (!decision.Allowed)
        {
            var errMsg = decision.DenialReason switch
            {
                "cap_reached" => isVi
                    ? $"Nhân vật đã đạt giới hạn cộng điểm thêm từ vật phẩm ({claimed:N0}/{decision.EffectiveCap:N0})."
                    : $"You have reached the extra stat consumable cap ({claimed:N0}/{decision.EffectiveCap:N0}).",
                "no_command" => isVi
                    ? "Chủng tộc này không dùng chỉ số Mệnh Lệnh (Command)."
                    : "Your character class does not use the Command stat.",
                _ => isVi
                    ? "Không thể sử dụng vật phẩm này."
                    : "Cannot use this stat item.",
            };
            await player.ShowBlueMessageAsync(errMsg).ConfigureAwait(false);
            return (true, false);
        }

        var targetAttr = decision.TargetBucket switch
        {
            "str" => Stats.BaseStrength,
            "agi" => Stats.BaseAgility,
            "vit" => Stats.BaseVitality,
            "ene" => Stats.BaseEnergy,
            "cmd" => Stats.BaseLeadership,
            _ => null,
        };

        if (targetAttr is not null)
        {
            player.Attributes[targetAttr] += decision.PointsGranted;
            await player.InvokeViewPlugInAsync<IStatIncreaseResultPlugIn>(
                p => p.StatIncreaseResultAsync(targetAttr, (ushort)Math.Min(decision.PointsGranted, ushort.MaxValue))).ConfigureAwait(false);
        }
        else
        {
            character.LevelUpPoints += decision.PointsGranted;
            await player.InvokeViewPlugInAsync<IUpdateCharacterStatsPlugIn>(
                p => p.UpdateCharacterStatsAsync()).ConfigureAwait(false);
        }

        if (item.Durability > 0)
        {
            item.Durability -= 1;
        }

        var totalAfter = store.RecordGranted(charKey, decision.TargetBucket, decision.PointsGranted);
        var label = isVi ? decision.DisplayNameVi : decision.DisplayNameEn;
        var bucketLabel = decision.TargetBucket switch
        {
            "str" => isVi ? "Sức Mạnh" : "Strength",
            "agi" => isVi ? "Nhanh Nhẹn" : "Agility",
            "vit" => isVi ? "Thể Lực" : "Vitality",
            "ene" => isVi ? "Năng Lượng" : "Energy",
            "cmd" => isVi ? "Mệnh Lệnh" : "Command",
            _ => isVi ? "điểm tiềm năng" : "Level-Up Points",
        };
        await player.ShowBlueMessageAsync(isVi
            ? $"[{label}] +{decision.PointsGranted:N0} {bucketLabel} (Tổng thưởng: {totalAfter:N0}/{decision.EffectiveCap:N0})."
            : $"[{label}] +{decision.PointsGranted:N0} {bucketLabel} (Total bonus: {totalAfter:N0}/{decision.EffectiveCap:N0}).").ConfigureAwait(false);

        return (true, true);
    }
}
