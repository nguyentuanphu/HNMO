// <copyright file="ChaosRatesPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GameplayRules;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.PlugIns;

/// <summary>
/// Adjusts Chaos Machine recipe success rates per recipe and globally via <see cref="ChaosRatesConfiguration"/>
/// so both actual rolls and player crafting previews reflect admin settings (ROADMAP 14.4).
/// </summary>
[PlugIn]
[Display(Name = "Chaos machine rates", Description = "Per-recipe and global Chaos Machine success rate multiplier shown to players.")]
[Guid("3C9E1B47-5D28-4A6F-8E91-2B4C6D8A0E17")]
public sealed class ChaosRatesPlugIn :
    IFeaturePlugIn,
    ISupportCustomConfiguration<ChaosRatesConfiguration>,
    ISupportDefaultCustomConfiguration
{
    /// <inheritdoc />
    public ChaosRatesConfiguration? Configuration { get; set; }

    /// <summary>
    /// Computes the adjusted success rate percentage (0..100) for <paramref name="player"/> and <paramref name="recipeNumber"/>.
    /// </summary>
    /// <param name="player">Player performing or previewing the mix.</param>
    /// <param name="recipeNumber">Recipe number (<c>ItemCrafting.Number</c>).</param>
    /// <param name="baseSuccessRate">Base calculated rate after standard player bonus.</param>
    /// <returns>Adjusted rate (0..100).</returns>
    public static byte AdjustSuccessRate(Player? player, byte? recipeNumber, byte baseSuccessRate)
    {
        var cfg = player?.GameContext.FeaturePlugIns.GetPlugIn<ChaosRatesPlugIn>()?.Configuration;
        if (cfg is not { Enabled: true })
        {
            return baseSuccessRate;
        }

        var ov = recipeNumber is { } num
            ? cfg.RecipeOverrides.FirstOrDefault(r => r.RecipeNumber == num)
            : null;

        if (baseSuccessRate == 0 && ov?.SuccessRateOverridePercent is null)
        {
            return 0;
        }

        double effective = ov?.SuccessRateOverridePercent ?? baseSuccessRate;
        if (ov is not null && Math.Abs(ov.RateMultiplier - 1.0) > 0.0001)
        {
            effective *= Math.Clamp(ov.RateMultiplier, 0.1, 10.0);
        }

        if (ov?.SuccessRateOverridePercent is null || Math.Abs(cfg.ServerWideMultiplier - 1.0) > 0.0001)
        {
            if (ov?.SuccessRateOverridePercent is null && baseSuccessRate < 100)
            {
                effective *= Math.Clamp(cfg.ServerWideMultiplier, 0.1, 10.0);
            }
            else if (ov?.SuccessRateOverridePercent is not null && Math.Abs(cfg.ServerWideMultiplier - 1.0) > 0.0001)
            {
                effective *= Math.Clamp(cfg.ServerWideMultiplier, 0.1, 10.0);
            }
        }

        if (effective <= 0)
        {
            return 0;
        }

        return (byte)Math.Clamp((int)Math.Round(effective), 1, 100);
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new ChaosRatesConfiguration();
}
