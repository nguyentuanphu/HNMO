// <copyright file="MapRulesPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GameplayRules;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.PlugIns;

/// <summary>
/// Enforces per-map PvP on/off, PK penalty exemption, EXP bonus %, item drop %, and zen drop % (ROADMAP 14.1).
/// </summary>
[PlugIn]
[Display(Name = "Per-map gameplay rules", Description = "Overrides PvP, PK penalty, EXP, item drop, and zen drop percentages per map.")]
[Guid("2E6F3A91-8D4B-4C7E-9A15-6B8D0E4C1F32")]
public sealed class MapRulesPlugIn :
    IFeaturePlugIn,
    IPlayerAttackRestrictionPlugIn,
    IPlayerKillPenaltyExemptionPlugIn,
    IExperienceCalculationPlugIn,
    ISupportCustomConfiguration<MapRulesConfiguration>,
    ISupportDefaultCustomConfiguration
{
    /// <inheritdoc />
    public MapRulesConfiguration? Configuration { get; set; }

    /// <summary>Gets the active <see cref="MapRulesPlugIn"/> from <paramref name="gameContext"/>.</summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The active plug-in, or <c>null</c>.</returns>
    public static MapRulesPlugIn? Get(IGameContext? gameContext) =>
        gameContext?.FeaturePlugIns.GetPlugIn<MapRulesPlugIn>()
        ?? gameContext?.PlugInManager.GetActivePlugInsOf<IExperienceCalculationPlugIn>().OfType<MapRulesPlugIn>().FirstOrDefault();

    /// <summary>Gets item and zen drop multipliers for <paramref name="player"/>'s current map.</summary>
    /// <param name="player">The player.</param>
    /// <returns>Tuple of <c>(ItemDropMultiplier, ZenDropMultiplier)</c>.</returns>
    public static (double ItemDropMultiplier, double ZenDropMultiplier) GetDropModifiers(Player? player)
    {
        if (player?.CurrentMap?.Definition is not { } map)
        {
            return (1.0, 1.0);
        }

        var rule = Get(player.GameContext)?.FindRule(map.Number);
        if (rule is null)
        {
            return (1.0, 1.0);
        }

        return (PercentToMultiplier(rule.ItemDropBonusPercent), PercentToMultiplier(rule.ZenDropBonusPercent));
    }

    /// <summary>Converts a percentage delta (-90..2000) to a multiplier (e.g., 50 -> 1.5).</summary>
    /// <param name="bonusPercent">Bonus percentage.</param>
    /// <returns>Multiplier.</returns>
    public static double PercentToMultiplier(int bonusPercent) => Math.Max(0.1, (100.0 + bonusPercent) / 100.0);

    /// <summary>Finds the matching rule for <paramref name="mapNumber"/> when enabled.</summary>
    /// <param name="mapNumber">Target map number.</param>
    /// <returns>Matching rule, or <c>null</c>.</returns>
    public MapRuleEntry? FindRule(short mapNumber)
    {
        if (this.Configuration is not { Enabled: true, Rules.Count: > 0 } cfg)
        {
            return null;
        }

        return cfg.Rules.FirstOrDefault(r => r.MapNumber == mapNumber);
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new MapRulesConfiguration();

    /// <inheritdoc />
    public void CheckAttack(Player attacker, Player defender, CancelEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        if (defender.CurrentMap?.Definition is not { } map)
        {
            return;
        }

        if (this.FindRule(map.Number) is { PvpEnabled: false })
        {
            eventArgs.Cancel = true;
        }
    }

    /// <inheritdoc />
    public void CheckExemption(Player attacker, Player defender, PlayerKillPenaltyExemptionArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        if (attacker.CurrentMap?.Definition is not { } map)
        {
            return;
        }

        if (this.FindRule(map.Number) is { PkPenaltyEnabled: false })
        {
            eventArgs.IsExempted = true;
        }
    }

    /// <inheritdoc />
    public ValueTask CalculateExperienceAsync(Player player, ExperienceCalculationArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (player.CurrentMap?.Definition is not { } map)
        {
            return ValueTask.CompletedTask;
        }

        if (this.FindRule(map.Number) is { ExpBonusPercent: not 0 } rule)
        {
            args.Experience *= PercentToMultiplier(rule.ExpBonusPercent);
        }

        return ValueTask.CompletedTask;
    }
}
