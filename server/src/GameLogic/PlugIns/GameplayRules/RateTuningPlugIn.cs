// <copyright file="RateTuningPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GameplayRules;

using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Bots;
using HNMO.GameLogic.Views;
using HNMO.Interfaces;
using HNMO.PlugIns;

/// <summary>
/// Server-wide jewel upgrade rate tuning, Life +Option cap (+16 vs +28), Chaos +15 announcement,
/// Zen drop scaling, and per-class PvP/PvM damage multipliers.
/// </summary>
[PlugIn]
[Display(Name = "Rate & Balance Tuning", Description = "Jewel of Soul/Life rates, max Life option cap (+16/+28), +15 Chaos broadcast, Zen multiplier, and class PvP/PvM damage scaling.")]
[Guid("9B42D1E8-6A3C-4F1D-8E55-1C7A2B9D4E20")]
public sealed class RateTuningPlugIn :
    IFeaturePlugIn,
    ISupportCustomConfiguration<RateTuningConfiguration>,
    ISupportDefaultCustomConfiguration
{
    /// <inheritdoc />
    public RateTuningConfiguration? Configuration { get; set; }

    /// <summary>
    /// Resolves effective Jewel of Soul success percentage and Luck bonus percentage for <paramref name="player"/>.
    /// </summary>
    /// <param name="player">Player consuming Jewel of Soul.</param>
    /// <param name="defaultSuccessPercent">Default handler success percentage.</param>
    /// <param name="defaultLuckBonusPercent">Default handler Luck bonus percentage.</param>
    /// <returns>Effective (SuccessPercent, LuckBonusPercent).</returns>
    public static (int SuccessPercent, int LuckBonusPercent) GetEffectiveSoulRates(
        Player? player,
        int defaultSuccessPercent,
        int defaultLuckBonusPercent)
    {
        var cfg = player?.GameContext.FeaturePlugIns.GetPlugIn<RateTuningPlugIn>()?.Configuration;
        if (cfg is not { Enabled: true, OverrideSoulRates: true })
        {
            return (defaultSuccessPercent, defaultLuckBonusPercent);
        }

        return (
            Math.Clamp(cfg.SoulSuccessRatePercent, 0, 100),
            Math.Clamp(cfg.SoulLuckBonusPercent, 0, 100));
    }

    /// <summary>
    /// Resolves effective Jewel of Life success chance (0..1) and optional max option level cap (1..7) for <paramref name="player"/>.
    /// </summary>
    /// <param name="player">Player consuming Jewel of Life.</param>
    /// <param name="defaultSuccessChance">Default handler success chance (0..1).</param>
    /// <returns>Effective success chance and nullable option cap.</returns>
    public static (double SuccessChance, int? MaxOptionLevel) GetEffectiveLifeOptionSettings(
        Player? player,
        double defaultSuccessChance)
    {
        var cfg = player?.GameContext.FeaturePlugIns.GetPlugIn<RateTuningPlugIn>()?.Configuration;
        if (cfg is not { Enabled: true })
        {
            return (defaultSuccessChance, null);
        }

        var chance = cfg.OverrideLifeRate
            ? Math.Clamp(cfg.LifeSuccessRatePercent, 0, 100) / 100.0
            : defaultSuccessChance;
        var cap = Math.Clamp(cfg.MaxLifeOptionLevel, 1, 7);
        return (chance, cap);
    }

    /// <summary>Returns the configured Zen drop multiplier for <paramref name="player"/>.</summary>
    /// <param name="player">Killer player.</param>
    /// <returns>Zen drop multiplier (1.0 when disabled).</returns>
    public static double GetZenDropMultiplier(Player? player)
    {
        var cfg = player?.GameContext.FeaturePlugIns.GetPlugIn<RateTuningPlugIn>()?.Configuration;
        if (cfg is not { Enabled: true })
        {
            return 1.0;
        }

        return Math.Clamp(cfg.ZenDropMultiplierPercent, 10, 1000) / 100.0;
    }

    /// <summary>
    /// Returns the multiplier of the base EXP of a kill for a party, when party EXP bonuses are configured;
    /// otherwise <see langword="null"/> (HNMO's own rule).
    /// </summary>
    /// <param name="recipients">The party members who get EXP of the kill.</param>
    /// <returns>The multiplier of the kill's base EXP, or <see langword="null"/>.</returns>
    public static double? GetPartyExpMultiplier(IReadOnlyList<Player> recipients)
    {
        var cfg = recipients.Count > 0 ? recipients[0].GameContext.FeaturePlugIns.GetPlugIn<RateTuningPlugIn>()?.Configuration : null;
        if (cfg is not { Enabled: true } || cfg.PartyExpBonuses.Count == 0
            || cfg.PartyExpBonuses.Find(e => e.PartySize == recipients.Count) is not { } entry)
        {
            return null;
        }

        var differentClasses = recipients.Count >= 3
            && recipients.Select(p => p.SelectedCharacter?.CharacterClass?.Number).Where(n => n.HasValue)
                .Select(n => ResolveClassLine(n!.Value)).Distinct().Count() == recipients.Count;
        var percent = differentClasses && entry.DifferentClassesExpPercent > 0 ? entry.DifferentClassesExpPercent : entry.ExpPercent;
        return Math.Max(0, percent) / 100.0;
    }

    /// <summary>
    /// Returns the zen a monster drops on the player's map from the zen table, when the table is on and lists the map;
    /// otherwise <see langword="null"/> (the zen then comes from the gained experience).
    /// </summary>
    /// <param name="player">Killer player.</param>
    /// <param name="monsterLevel">The killed monster's level.</param>
    /// <param name="randomizer">The randomizer.</param>
    /// <returns>The zen amount, or <see langword="null"/>.</returns>
    public static uint? GetTableZen(Player? player, int monsterLevel, IRandomizer randomizer)
    {
        var cfg = player?.GameContext.FeaturePlugIns.GetPlugIn<RateTuningPlugIn>()?.Configuration;
        if (cfg is not { Enabled: true, ZenTableEnabled: true } || player?.CurrentMap?.Definition is not { } map
            || cfg.ZenTable.Find(e => e.MapNumber == map.Number) is not { } entry)
        {
            return null;
        }

        var minimum = Math.Max(0, Math.Min(entry.MinimumZen, entry.MaximumZen));
        var maximum = Math.Max(minimum, entry.MaximumZen);
        long zen = randomizer.NextInt(minimum, maximum + 1);
        if (cfg.ZenTableMultiplyByMonsterLevel
            && randomizer.NextRandomBool(Math.Clamp(cfg.ZenTableMultiplyChancePerTenThousand, 0, 10000) / 10000.0))
        {
            zen *= Math.Max(1, monsterLevel);
        }

        return (uint)Math.Clamp(zen, 0, int.MaxValue);
    }

    /// <summary>Maps any 1st/2nd/3rd job class number to its canonical base class line number.</summary>
    /// <param name="classNumber">Character class number.</param>
    /// <returns>Canonical class line number.</returns>
    public static byte ResolveClassLine(byte classNumber) => classNumber switch
    {
        BotClassNumbers.DarkWizardNumber or BotClassNumbers.SoulMasterNumber or BotClassNumbers.GrandMasterNumber => BotClassNumbers.DarkWizardNumber,
        BotClassNumbers.DarkKnightNumber or BotClassNumbers.BladeKnightNumber or BotClassNumbers.BladeMasterNumber => BotClassNumbers.DarkKnightNumber,
        BotClassNumbers.FairyElfNumber or BotClassNumbers.MuseElfNumber or BotClassNumbers.HighElfNumber => BotClassNumbers.FairyElfNumber,
        BotClassNumbers.MagicGladiatorNumber or BotClassNumbers.DuelMasterNumber => BotClassNumbers.MagicGladiatorNumber,
        BotClassNumbers.DarkLordNumber or BotClassNumbers.LordEmperorNumber => BotClassNumbers.DarkLordNumber,
        BotClassNumbers.SummonerNumber or BotClassNumbers.BloodySummonerNumber or BotClassNumbers.DimensionMasterNumber => BotClassNumbers.SummonerNumber,
        BotClassNumbers.RageFighterNumber or BotClassNumbers.FistMasterNumber => BotClassNumbers.RageFighterNumber,
        _ => classNumber,
    };

    /// <summary>Applies per-class PvP or PvM damage scaling for <paramref name="attacker"/>.</summary>
    /// <param name="attacker">Attacker entity.</param>
    /// <param name="isPvp">Whether the target is another player.</param>
    /// <param name="damage">Calculated damage prior to scaling.</param>
    /// <returns>Scaled damage (minimum 1 when positive).</returns>
    public static int ApplyClassDamageMultiplier(IAttacker attacker, bool isPvp, int damage)
    {
        if (damage <= 0)
        {
            return damage;
        }

        var player = attacker as Player ?? (attacker as IPlayerSurrogate)?.Owner;
        var character = player?.SelectedCharacter;
        if (player is null || character?.CharacterClass is not { } cls)
        {
            return damage;
        }

        var cfg = player.GameContext.FeaturePlugIns.GetPlugIn<RateTuningPlugIn>()?.Configuration;
        if (cfg is not { Enabled: true } || cfg.ClassDamageRates.Count == 0)
        {
            return damage;
        }

        var line = ResolveClassLine(cls.Number);
        var entry = cfg.ClassDamageRates.FirstOrDefault(e => e.ClassLine == line);
        if (entry is null)
        {
            return damage;
        }

        var percent = Math.Clamp(isPvp ? entry.PvpDamagePercent : entry.PvmDamagePercent, 10, 500);
        if (percent == 100)
        {
            return damage;
        }

        return Math.Max(1, (int)Math.Round(damage * (percent / 100.0)));
    }

    /// <summary>
    /// Broadcasts a global server announcement if a Chaos Machine craft produced an item at or above <see cref="RateTuningConfiguration.BroadcastMinChaosMixLevel"/>.
    /// </summary>
    /// <param name="player">Player who completed the mix.</param>
    /// <param name="craftedItem">Resulting crafted item, if any.</param>
    /// <returns>A task representing the asynchronous broadcast.</returns>
    public static async ValueTask TryBroadcastChaosMixSuccessAsync(Player player, Item? craftedItem)
    {
        if (craftedItem is null)
        {
            return;
        }

        var cfg = player.GameContext.FeaturePlugIns.GetPlugIn<RateTuningPlugIn>()?.Configuration;
        if (cfg is not { Enabled: true, BroadcastHighLevelChaosMix: true })
        {
            return;
        }

        var minLevel = Math.Clamp(cfg.BroadcastMinChaosMixLevel, 10, 15);
        if (craftedItem.Level < minLevel)
        {
            return;
        }

        var playerName = player.SelectedCharacter?.Name ?? player.Account?.LoginName ?? "Hero";
        await player.GameContext.ForEachPlayerAsync(async recipient =>
        {
            var isVi = string.Equals(recipient.Culture.TwoLetterISOLanguageName, "vi", StringComparison.OrdinalIgnoreCase);
            var itemName = craftedItem.Definition?.Name.GetTranslation(recipient.Culture) ?? "Item";
            var msg = isVi
                ? $"[Chaos] {playerName} đã ép thành công {itemName} +{craftedItem.Level}!"
                : $"[Chaos] {playerName} successfully crafted {itemName} +{craftedItem.Level}!";
            await recipient.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(msg, MessageType.GoldenCenter)).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new RateTuningConfiguration();
}
