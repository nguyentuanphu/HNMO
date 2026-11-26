// <copyright file="SeasonNineSystemsEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.SeasonNineSystems;

using System;
using System.Collections.Generic;
using System.Linq;
using HNMO.DataModel.Configuration;

/// <summary>Pentagram elemental affinities for Season 9 elemental combat.</summary>
public enum PentagramElement
{
    /// <summary>No element.</summary>
    Undefined = 0,

    /// <summary>Fire element.</summary>
    Fire = 1,

    /// <summary>Water element.</summary>
    Water = 2,

    /// <summary>Earth element.</summary>
    Earth = 3,

    /// <summary>Wind element.</summary>
    Wind = 4,

    /// <summary>Darkness element.</summary>
    Darkness = 5,
}

/// <summary>
/// Pure Season 9 calculation engine for Muun option scaling &amp; conditions, Pentagram elemental advantage,
/// Errtel socket extraction/option scaling, and skill 618-621 mechanics.
/// </summary>
public static class SeasonNineSystemsEngine
{
    private static readonly Dictionary<(byte Rank, byte Level), int> ExtractRateMap =
        SeasonNineSystemsCatalog.ErrtelExtractionRates.ToDictionary(r => (r.Rank, r.Level), r => r.RateOutOf10000);

    private static readonly Dictionary<int, SeasonNineSystemsCatalog.MuunOptionSpec> MuunOptionMap =
        SeasonNineSystemsCatalog.MuunOptions.ToDictionary(o => o.Index);

    /// <summary>Looks up a Muun pet specification by item group &amp; number.</summary>
    /// <param name="group">Item group (typically 13).</param>
    /// <param name="number">Item number.</param>
    /// <returns>Matching <see cref="SeasonNineSystemsCatalog.MuunSpec"/> or <c>null</c>.</returns>
    public static SeasonNineSystemsCatalog.MuunSpec? FindMuunByItem(byte group, short number)
    {
        foreach (var m in SeasonNineSystemsCatalog.Muuns)
        {
            if (m.ItemGroup == group && m.ItemNumber == number)
            {
                return m;
            }
        }

        return null;
    }

    /// <summary>Looks up a Muun option by its index.</summary>
    /// <param name="index">The option index of a <see cref="SeasonNineSystemsCatalog.MuunSpec"/>.</param>
    /// <returns>The option, or <c>null</c>.</returns>
    public static SeasonNineSystemsCatalog.MuunOptionSpec? FindMuunOption(int index) =>
        MuunOptionMap.TryGetValue(index, out var option) ? option : null;

    /// <summary>The highest level a Muun reaches before it can evolve: its option's last level with a value (1..5).</summary>
    /// <param name="muun">The Muun.</param>
    /// <returns>The level.</returns>
    public static int MuunMaximumLevel(SeasonNineSystemsCatalog.MuunSpec muun) =>
        FindMuunOption(muun.OptionIndex) is { } option ? MuunMaximumLevel(option) : 1;

    /// <summary>The highest level of a Muun option: its last level with a value (1..5).</summary>
    /// <param name="option">The option.</param>
    /// <returns>The level.</returns>
    public static int MuunMaximumLevel(SeasonNineSystemsCatalog.MuunOptionSpec option)
    {
        int[] values = [option.ValueLv1, option.ValueLv2, option.ValueLv3, option.ValueLv4, option.ValueLv5];
        for (var level = values.Length; level > 1; level--)
        {
            if (values[level - 1] > 0)
            {
                return level;
            }
        }

        return 1;
    }

    /// <summary>Checks whether a Muun option's time/map/level/day condition is currently satisfied.</summary>
    /// <param name="option">Option spec.</param>
    /// <param name="mapNumber">Current map number.</param>
    /// <param name="totalLevel">Character level + master level.</param>
    /// <param name="localNow">Local server time.</param>
    /// <returns><see langword="true"/> when condition holds.</returns>
    public static bool IsMuunConditionMet(
        SeasonNineSystemsCatalog.MuunOptionSpec option,
        int mapNumber,
        int totalLevel,
        DateTime localNow)
    {
        switch (option.ConditionType)
        {
            case 1: // Map match
                return option.ConditionValue1 < 0 || mapNumber == option.ConditionValue1;
            case 2: // Level range (normal + master)
                return totalLevel >= option.ConditionValue1 && totalLevel <= option.ConditionValue2;
            case 3: // Hour range (1..24)
            {
                var hour = localNow.Hour == 0 ? 24 : localNow.Hour;
                return hour >= option.ConditionValue1 && hour <= option.ConditionValue2;
            }
            case 4: // Weekday bitmask: Sun=64, Mon=32, Tue=16, Wed=8, Thu=4, Fri=2, Sat=1
            {
                if (option.ConditionValue1 < 0)
                {
                    return true;
                }

                var bit = localNow.DayOfWeek switch
                {
                    DayOfWeek.Sunday => 64,
                    DayOfWeek.Monday => 32,
                    DayOfWeek.Tuesday => 16,
                    DayOfWeek.Wednesday => 8,
                    DayOfWeek.Thursday => 4,
                    DayOfWeek.Friday => 2,
                    DayOfWeek.Saturday => 1,
                    _ => 0,
                };
                return (option.ConditionValue1 & bit) != 0;
            }
            default:
                return true;
        }
    }

    /// <summary>Evaluates active bonus value for a Muun pet at <paramref name="muunLevel"/> (0 = evolved, 1..5 = rank level).</summary>
    /// <param name="muun">Muun definition.</param>
    /// <param name="muunLevel">Current Muun level (1..5 or evolved).</param>
    /// <param name="evolved">Whether the Muun is evolved.</param>
    /// <param name="mapNumber">Player current map number.</param>
    /// <param name="totalLevel">Player level + master level.</param>
    /// <param name="localNow">Local server time.</param>
    /// <param name="strictConditions">Whether map/time/day conditions are enforced.</param>
    /// <returns>Scaled bonus value, or <c>0</c> if condition fails.</returns>
    public static int EvaluateMuunBonus(
        SeasonNineSystemsCatalog.MuunSpec muun,
        int muunLevel,
        bool evolved,
        int mapNumber,
        int totalLevel,
        DateTime localNow,
        bool strictConditions = true)
    {
        return MuunOptionMap.TryGetValue(muun.OptionIndex, out var opt)
            ? EvaluateMuunBonus(muun, opt, muunLevel, evolved, mapNumber, totalLevel, localNow, strictConditions)
            : 0;
    }

    /// <summary>Evaluates the active bonus value of a Muun with a given option (e.g. one of the admin's table).</summary>
    /// <param name="muun">Muun definition.</param>
    /// <param name="opt">The Muun's option.</param>
    /// <param name="muunLevel">Current Muun level (1..5).</param>
    /// <param name="evolved">Whether the Muun is evolved.</param>
    /// <param name="mapNumber">Player current map number.</param>
    /// <param name="totalLevel">Player level + master level.</param>
    /// <param name="localNow">Local server time.</param>
    /// <param name="strictConditions">Whether map/time/day conditions are enforced.</param>
    /// <returns>Scaled bonus value, or <c>0</c> if condition fails.</returns>
    public static int EvaluateMuunBonus(
        SeasonNineSystemsCatalog.MuunSpec muun,
        SeasonNineSystemsCatalog.MuunOptionSpec opt,
        int muunLevel,
        bool evolved,
        int mapNumber,
        int totalLevel,
        DateTime localNow,
        bool strictConditions = true)
    {
        if (strictConditions && !IsMuunConditionMet(opt, mapNumber, totalLevel, localNow))
        {
            return 0;
        }

        var baseVal = evolved
            ? opt.ValueEvolved
            : Math.Clamp(muunLevel, 1, 5) switch
            {
                1 => opt.ValueLv1,
                2 => opt.ValueLv2,
                3 => opt.ValueLv3,
                4 => opt.ValueLv4,
                _ => opt.ValueLv5,
            };

        if (baseVal <= 0)
        {
            return opt.ValueLv1;
        }

        return muun.AddOptionType switch
        {
            1 => baseVal * Math.Max(1, muun.AddOptionValue) / 10 + baseVal,
            2 => baseVal + muun.AddOptionValue,
            _ => baseVal,
        };
    }

    /// <summary>Computes Muun Energy Converter points yielded by converting a Muun of given rank and level.</summary>
    /// <param name="rank">Muun rank (1..6).</param>
    /// <param name="level">Muun level (0..7).</param>
    /// <param name="multiplier">Admin-configured multiplier.</param>
    /// <returns>Energy points gained.</returns>
    public static int CalculateMuunEnergyConversionPoints(int rank, int level, double multiplier)
    {
        var rankPts = Math.Clamp(rank, 1, 6);
        var levelPts = level <= 0 ? 4 : Math.Clamp(level, 1, 7) * 3;
        return Math.Max(1, (int)Math.Round((rankPts + levelPts) * Math.Max(0.1, multiplier)));
    }

    /// <summary>Computes elemental advantage multiplier (Fire &gt; Darkness &gt; Wind &gt; Earth &gt; Water &gt; Fire).</summary>
    /// <param name="attacker">Attacker element.</param>
    /// <param name="defender">Defender element.</param>
    /// <param name="advantageBonusPct">Advantage % (default 15).</param>
    /// <param name="baseMultiplier">Global elemental damage multiplier.</param>
    /// <returns>Damage multiplier.</returns>
    public static double CalculateElementalMultiplier(
        PentagramElement attacker,
        PentagramElement defender,
        int advantageBonusPct = 15,
        double baseMultiplier = 1.0)
    {
        if (attacker == PentagramElement.Undefined || defender == PentagramElement.Undefined)
        {
            return baseMultiplier;
        }

        if (defender == StrongAgainst(attacker))
        {
            return baseMultiplier * (1.0 + Math.Max(0, advantageBonusPct) / 100.0);
        }

        if (attacker == StrongAgainst(defender))
        {
            return baseMultiplier * Math.Max(0.5, 1.0 - Math.Max(0, advantageBonusPct) / 200.0);
        }

        return baseMultiplier;
    }

    private static PentagramElement StrongAgainst(PentagramElement element) => element switch
    {
        PentagramElement.Fire => PentagramElement.Darkness,
        PentagramElement.Darkness => PentagramElement.Wind,
        PentagramElement.Wind => PentagramElement.Earth,
        PentagramElement.Earth => PentagramElement.Water,
        PentagramElement.Water => PentagramElement.Fire,
        _ => PentagramElement.Undefined,
    };

    /// <summary>Gets Errtel extraction success chance out of 10000 for <paramref name="rank"/> and <paramref name="level"/>.</summary>
    /// <param name="rank">Errtel rank (1..5).</param>
    /// <param name="level">Errtel level (0..10).</param>
    /// <param name="bonusPct">Admin bonus percentage added on top.</param>
    /// <returns>Clamped success chance out of 10000.</returns>
    public static int CalculateErrtelExtractionRate(byte rank, byte level, int bonusPct)
    {
        var baseRate = ExtractRateMap.TryGetValue((rank, level), out var rate) ? rate : 7000;
        return Math.Clamp(baseRate + (bonusPct * 100), 0, 10000);
    }

    /// <summary>Computes Shield-Burn (621) SD reduction.</summary>
    /// <param name="currentSd">Target current shield defense.</param>
    /// <param name="percent">Configured percentage (0..100).</param>
    /// <returns>SD drained.</returns>
    public static int CalculateShieldBurnReduction(int currentSd, int percent)
    {
        if (currentSd <= 0 || percent <= 0)
        {
            return 0;
        }

        return (int)Math.Ceiling(currentSd * Math.Clamp(percent, 1, 100) / 100.0);
    }
}
