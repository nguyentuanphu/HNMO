// <copyright file="MiningSystemEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.S6Events;

using System;
using System.Collections.Generic;

/// <summary>Mining twinkle tiers corresponding to NPC IDs 605..607.</summary>
public enum MiningTwinkleType : short
{
    /// <summary>Mining Area (Small), NPC 605, 2 durability per swing.</summary>
    Small = 605,

    /// <summary>Mining Area (Medium), NPC 606, 4 durability per swing.</summary>
    Medium = 606,

    /// <summary>Mining Area (Large), NPC 607, 8 durability per swing.</summary>
    Large = 607,
}

/// <summary>
/// Pure calculation engine for Pandora Pick (0-41) twinkle mining, stage 1..5 jewel payouts,
/// Pandora Stage 5 jackpots, and Jewel of Bless / bundle repairs (<c>IGC_MiningSystem.xml</c>).
/// </summary>
public static class MiningSystemEngine
{
    /// <summary>Gets Pandora Pick durability consumed per mining attempt.</summary>
    /// <param name="twinkle">Twinkle type.</param>
    /// <returns>Durability decrement (2, 4, or 8).</returns>
    public static int GetDurabilityCost(MiningTwinkleType twinkle) => twinkle switch
    {
        MiningTwinkleType.Small => 2,
        MiningTwinkleType.Medium => 4,
        MiningTwinkleType.Large => 8,
        _ => 2,
    };

    /// <summary>Gets configured stage success rate per million for <paramref name="twinkle"/> and 1-based <paramref name="stage"/>.</summary>
    /// <param name="cfg">S6 events configuration.</param>
    /// <param name="twinkle">Twinkle type.</param>
    /// <param name="stage">Mining stage (1..5).</param>
    /// <returns>Rate out of <see cref="S6EventsConfiguration.MiningChanceDenominator"/>.</returns>
    public static int GetStageSuccessRate(S6EventsConfiguration cfg, MiningTwinkleType twinkle, int stage)
    {
        ArgumentNullException.ThrowIfNull(cfg);
        var idx = Math.Clamp(stage - 1, 0, 4);
        var rates = twinkle switch
        {
            MiningTwinkleType.Small => cfg.MiningSmallRates,
            MiningTwinkleType.Medium => cfg.MiningMediumRates,
            MiningTwinkleType.Large => cfg.MiningLargeRates,
            _ => cfg.MiningSmallRates,
        };
        return idx < rates.Count ? rates[idx] : 475_000;
    }

    /// <summary>Gets Pandora jackpot rate at stage 5 for <paramref name="twinkle"/>.</summary>
    /// <param name="cfg">Configuration.</param>
    /// <param name="twinkle">Twinkle type.</param>
    /// <returns>Rate out of <see cref="S6EventsConfiguration.MiningChanceDenominator"/>.</returns>
    public static int GetPandoraJackpotRate(S6EventsConfiguration cfg, MiningTwinkleType twinkle)
    {
        ArgumentNullException.ThrowIfNull(cfg);
        var idx = twinkle switch
        {
            MiningTwinkleType.Small => 0,
            MiningTwinkleType.Medium => 1,
            MiningTwinkleType.Large => 2,
            _ => 0,
        };
        return idx < cfg.MiningPandoraRates.Count ? cfg.MiningPandoraRates[idx] : 18_000;
    }

    /// <summary>Gets total Jewel of Bless count awarded on winning <paramref name="twinkle"/> at <paramref name="stage"/>.</summary>
    /// <param name="twinkle">Twinkle type.</param>
    /// <param name="stage">Stage (1..5).</param>
    /// <param name="isPandoraJackpot">Whether stage 5 Pandora bonus triggered.</param>
    /// <returns>Total Bless jewels represented as bundles + singles.</returns>
    public static int GetRewardBlessTotal(MiningTwinkleType twinkle, int stage, bool isPandoraJackpot)
    {
        if (stage == 5 && isPandoraJackpot)
        {
            return twinkle switch
            {
                MiningTwinkleType.Small => 100,
                MiningTwinkleType.Medium => 400,
                MiningTwinkleType.Large => 1000,
                _ => 100,
            };
        }

        return (twinkle, Math.Clamp(stage, 1, 5)) switch
        {
            (MiningTwinkleType.Small, 1) => 1,
            (MiningTwinkleType.Small, 2) => 3,
            (MiningTwinkleType.Small, 3) => 7,
            (MiningTwinkleType.Small, 4) => 16,
            (MiningTwinkleType.Small, 5) => 33,
            (MiningTwinkleType.Medium, 1) => 2,
            (MiningTwinkleType.Medium, 2) => 6,
            (MiningTwinkleType.Medium, 3) => 15,
            (MiningTwinkleType.Medium, 4) => 34,
            (MiningTwinkleType.Medium, 5) => 67,
            (MiningTwinkleType.Large, 1) => 4,
            (MiningTwinkleType.Large, 2) => 12,
            (MiningTwinkleType.Large, 3) => 29,
            (MiningTwinkleType.Large, 4) => 65,
            (MiningTwinkleType.Large, 5) => 125,
            _ => 1,
        };
    }

    /// <summary>Decomposes a Bless total into Bundled +30 (`12:30` Lv2), +20 (`Lv1`), +10 (`Lv0`), and loose `14:13` pieces.</summary>
    /// <param name="totalBless">Total Bless jewel count.</param>
    /// <returns>List of <see cref="S6EventsOwedReward"/> entries to grant.</returns>
    public static List<S6EventsOwedReward> BuildBlessPayoutItems(int totalBless)
    {
        var result = new List<S6EventsOwedReward>();
        var remaining = Math.Max(0, totalBless);
        var bundles30 = remaining / 30;
        remaining %= 30;
        if (bundles30 > 0)
        {
            result.Add(new S6EventsOwedReward
            {
                ItemGroup = 12,
                ItemNumber = 30,
                ItemLevel = 2,
                ItemCount = Math.Min(bundles30, 20),
                ReasonEn = $"Mining Bundle of Bless +30 x{bundles30}",
                ReasonVi = $"Đào Khoáng: Cụm Bless +30 x{bundles30}",
            });

            var overflow = bundles30 - 20;
            while (overflow > 0)
            {
                var batch = Math.Min(overflow, 20);
                result.Add(new S6EventsOwedReward
                {
                    ItemGroup = 12,
                    ItemNumber = 30,
                    ItemLevel = 2,
                    ItemCount = batch,
                    ReasonEn = $"Mining Bundle of Bless +30 x{batch}",
                    ReasonVi = $"Đào Khoáng: Cụm Bless +30 x{batch}",
                });
                overflow -= batch;
            }
        }

        if (remaining >= 20)
        {
            result.Add(new S6EventsOwedReward { ItemGroup = 12, ItemNumber = 30, ItemLevel = 1, ItemCount = 1, ReasonEn = "Mining Bundle of Bless +20", ReasonVi = "Đào Khoáng: Cụm Bless +20" });
            remaining -= 20;
        }
        else if (remaining >= 10)
        {
            result.Add(new S6EventsOwedReward { ItemGroup = 12, ItemNumber = 30, ItemLevel = 0, ItemCount = 1, ReasonEn = "Mining Bundle of Bless +10", ReasonVi = "Đào Khoáng: Cụm Bless +10" });
            remaining -= 10;
        }

        if (remaining > 0)
        {
            result.Add(new S6EventsOwedReward { ItemGroup = 14, ItemNumber = 13, ItemLevel = 0, ItemCount = remaining, ReasonEn = $"Mining Jewel of Bless x{remaining}", ReasonVi = $"Đào Khoáng: Ngọc Ước Nguyện x{remaining}" });
        }

        return result;
    }

    /// <summary>Gets Pandora Pick durability restored by a single Jewel of Bless or Bundle item.</summary>
    /// <param name="group">Item group.</param>
    /// <param name="number">Item number.</param>
    /// <param name="level">Item level.</param>
    /// <returns>Repair amount (4 for 14:13, 40/80/120 for 12:30 lv 0/1/2), or 0.</returns>
    public static int GetRepairAmount(int group, short number, byte level) => (group, number, level) switch
    {
        (14, 13, _) => 4,
        (12, 30, 0) => 40,
        (12, 30, 1) => 80,
        (12, 30, >= 2) => 120,
        _ => 0,
    };
}
