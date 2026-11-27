// <copyright file="MossGambleCatalog.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.MossMerchant;

using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;

/// <summary>
/// Reward pools and tier probabilities for Moss the Merchant (492) matching the Season 6/9 server suites.
/// </summary>
internal static class MossGambleCatalog
{
    private static readonly GambleTier[] SwordMaceSpearTiers =
    [
        Tier(560_000, 100_000, 50_000, 120_000, 5_000, Exc([(0, 1), (0, 4), (0, 6), (0, 7), (0, 8), (0, 9)])),
        Tier(380_000, 100_000, 50_000, 100_000, 3_000, Exc([(0, 0), (0, 2), (0, 3), (0, 5), (0, 10), (0, 11), (0, 12), (0, 13), (0, 14), (0, 15), (2, 3), (2, 4), (2, 5), (3, 7), (3, 8), (3, 9)])),
        Tier(50_000, 50_000, 50_000, 50_000, 1_000, Exc([(3, 10), (0, 16), (0, 17), (0, 18)])),
        Tier(9_980, 30_000, 10_000, 10_000, 0, Normal([(0, 20), (0, 21), (0, 24), (0, 25), (0, 31)])),
        Tier(20, 0, 0, 0, 0, [Fixed(14, 96)]),
    ];

    private static readonly GambleTier[] StaffTiers =
    [
        Tier(560_000, 100_000, 50_000, 120_000, 5_000, Exc([(5, 0), (5, 1), (5, 2)])),
        Tier(380_000, 100_000, 50_000, 100_000, 3_000, Exc([(5, 3), (5, 4)])),
        Tier(50_000, 50_000, 50_000, 50_000, 1_000, Exc([(5, 5), (5, 6), (5, 8)])),
        Tier(9_980, 30_000, 10_000, 10_000, 0, Normal([(5, 9), (5, 11), (5, 13)])),
        Tier(20, 0, 0, 0, 0, [Fixed(14, 96)]),
    ];

    private static readonly GambleTier[] BowCrossbowTiers =
    [
        Tier(560_000, 100_000, 50_000, 120_000, 5_000, Exc([(4, 0), (4, 1), (4, 2), (4, 3)])),
        Tier(380_000, 100_000, 50_000, 100_000, 3_000, Exc([(4, 4), (4, 5), (4, 9), (4, 10)])),
        Tier(50_000, 50_000, 50_000, 50_000, 1_000, [.. Exc([(4, 11), (4, 14), (4, 16)]), .. Normal([(4, 17), (4, 19), (4, 20)])]),
        Tier(9_980, 30_000, 10_000, 10_000, 0, Normal([(4, 22)])),
        Tier(20, 0, 0, 0, 0, [Fixed(14, 96)]),
    ];

    private static readonly GambleTier[] ScepterTiers =
    [
        Tier(560_000, 100_000, 50_000, 120_000, 5_000, Exc([(2, 8), (2, 9)])),
        Tier(380_000, 100_000, 50_000, 100_000, 3_000, Exc([(2, 10), (2, 11)])),
        Tier(50_000, 50_000, 50_000, 50_000, 1_000, Exc([(2, 12)])),
        Tier(9_980, 30_000, 10_000, 10_000, 0, Normal([(2, 15)])),
        Tier(20, 0, 0, 0, 0, [Fixed(14, 96)]),
    ];

    private static readonly GambleTier[] StickTiers =
    [
        Tier(560_000, 100_000, 50_000, 120_000, 5_000, Exc([(5, 14)])),
        Tier(380_000, 100_000, 50_000, 100_000, 3_000, Exc([(5, 15), (5, 16)])),
        Tier(50_000, 50_000, 50_000, 50_000, 1_000, Exc([(5, 17)])),
        Tier(9_980, 30_000, 10_000, 10_000, 0, Normal([(5, 18)])),
        Tier(20, 0, 0, 0, 0, [Fixed(14, 96)]),
    ];

    private static readonly int[] WeightedExcellentSlotRates =
    [
        100_000,
        200_000,
        200_000,
        200_000,
        200_000,
        100_000,
    ];

    /// <summary>Rolls a temporary reward item for voucher number 71..75 against the active game configuration.</summary>
    /// <param name="configuration">The game configuration.</param>
    /// <param name="voucherNumber">The gamble voucher number (71..75).</param>
    /// <param name="randomizer">The randomizer.</param>
    /// <param name="useWeightedExcellentSelection">Whether to use suite weighted Excellent bit selection (10/20/20/20/20/10%).</param>
    /// <returns>The rolled temporary reward item, or <see langword="null"/> if not a valid voucher.</returns>
    public static TemporaryItem? RollReward(
        GameConfiguration configuration,
        short voucherNumber,
        IRandomizer randomizer,
        bool useWeightedExcellentSelection = false)
    {
        var tiers = voucherNumber switch
        {
            71 => SwordMaceSpearTiers,
            72 => StaffTiers,
            73 => BowCrossbowTiers,
            74 => ScepterTiers,
            75 => StickTiers,
            _ => null,
        };
        if (tiers is null)
        {
            return null;
        }

        // Filter tiers down to items that exist in this configuration so partial test/server catalogs still roll cleanly.
        var availableTiers = new List<(GambleTier Tier, List<(GambleRewardSpec Spec, ItemDefinition Def)> Candidates)>();
        foreach (var tier in tiers)
        {
            var candidates = new List<(GambleRewardSpec, ItemDefinition)>();
            foreach (var spec in tier.Items)
            {
                if (configuration.Items.FirstOrDefault(i => i.Group == spec.Group && i.Number == spec.Number) is { } def)
                {
                    candidates.Add((spec, def));
                }
            }

            if (candidates.Count > 0)
            {
                availableTiers.Add((tier, candidates));
            }
        }

        if (availableTiers.Count == 0)
        {
            return null;
        }

        var totalWeight = availableTiers.Sum(entry => entry.Tier.Weight);
        var roll = randomizer.NextInt(0, totalWeight);
        var chosen = availableTiers[0];
        var cumulative = 0;
        foreach (var entry in availableTiers)
        {
            cumulative += entry.Tier.Weight;
            if (roll < cumulative)
            {
                chosen = entry;
                break;
            }
        }

        var (pickedSpec, pickedDef) = chosen.Candidates[randomizer.NextInt(0, chosen.Candidates.Count)];
        var item = new TemporaryItem
        {
            Definition = pickedDef,
            Durability = Math.Max((byte)1, pickedDef.Durability),
            Level = pickedSpec.MinLevel >= pickedSpec.MaxLevel
                ? pickedSpec.MinLevel
                : (byte)randomizer.NextInt(pickedSpec.MinLevel, pickedSpec.MaxLevel + 1),
        };

        if (pickedSpec.AllowSkill && pickedDef.Skill is not null && RollMillion(randomizer, chosen.Tier.SkillChancePerMillion))
        {
            item.HasSkill = true;
        }

        if (pickedSpec.AllowLuck && RollMillion(randomizer, chosen.Tier.LuckChancePerMillion))
        {
            var luck = pickedDef.PossibleItemOptions
                .SelectMany(o => o.PossibleOptions)
                .FirstOrDefault(o => Equals(o.OptionType, ItemOptionTypes.Luck));
            if (luck is not null)
            {
                item.ItemOptions.Add(new ItemOptionLink { ItemOption = luck });
            }
        }

        if (pickedSpec.AllowOption && RollMillion(randomizer, chosen.Tier.OptionChancePerMillion))
        {
            var addOption = pickedDef.PossibleItemOptions
                .SelectMany(o => o.PossibleOptions)
                .FirstOrDefault(o => Equals(o.OptionType, ItemOptionTypes.Option));
            if (addOption is not null)
            {
                // Option level weights: +4 (85%), +8 (10%), +12 (5%) per suite table.
                var optRoll = randomizer.NextInt(0, 100);
                var optLevel = optRoll < 85 ? 1 : optRoll < 95 ? 2 : 3;
                item.ItemOptions.Add(new ItemOptionLink { ItemOption = addOption, Level = optLevel });
            }
        }

        if (pickedSpec.AllowExcellent && RollMillion(randomizer, chosen.Tier.ExcellentChancePerMillion))
        {
            var excPool = pickedDef.PossibleItemOptions
                .SelectMany(o => o.PossibleOptions)
                .Where(o => Equals(o.OptionType, ItemOptionTypes.Excellent))
                .ToList();
            if (excPool.Count > 0)
            {
                // Suite table: 90% 2 options, 10% 3 options.
                var targetCount = Math.Min(excPool.Count, randomizer.NextInt(0, 10) < 9 ? 2 : 3);
                for (var i = 0; i < targetCount && excPool.Count > 0; i++)
                {
                    var idx = useWeightedExcellentSelection
                        ? PickWeightedExcellentIndex(excPool.Count, randomizer)
                        : randomizer.NextInt(0, excPool.Count);
                    item.ItemOptions.Add(new ItemOptionLink { ItemOption = excPool[idx] });
                    excPool.RemoveAt(idx);
                }
            }
        }

        return item;
    }

    private static int PickWeightedExcellentIndex(int poolCount, IRandomizer randomizer)
    {
        if (poolCount <= 1)
        {
            return 0;
        }

        var weights = new int[poolCount];
        var total = 0;
        for (var i = 0; i < poolCount; i++)
        {
            var weight = i < WeightedExcellentSlotRates.Length ? WeightedExcellentSlotRates[i] : 100_000;
            weights[i] = weight;
            total += weight;
        }

        var roll = randomizer.NextInt(0, total);
        var acc = 0;
        for (var i = 0; i < poolCount; i++)
        {
            acc += weights[i];
            if (roll < acc)
            {
                return i;
            }
        }

        return poolCount - 1;
    }

    private static bool RollMillion(IRandomizer randomizer, int chancePerMillion)
        => chancePerMillion > 0 && randomizer.NextInt(0, 1_000_000) < chancePerMillion;

    private static GambleTier Tier(int weight, int skill, int luck, int opt, int exc, IReadOnlyList<GambleRewardSpec> items)
        => new(weight, skill, luck, opt, exc, items);

    private static List<GambleRewardSpec> Exc((byte Group, short Number)[] entries)
        => entries.Select(e => new GambleRewardSpec(e.Group, e.Number, 0, 3, true, true, true, true)).ToList();

    private static List<GambleRewardSpec> Normal((byte Group, short Number)[] entries)
        => entries.Select(e => new GambleRewardSpec(e.Group, e.Number, 0, 3, true, true, true, false)).ToList();

    private static GambleRewardSpec Fixed(byte group, short number)
        => new(group, number, 0, 0, false, false, false, false);

    private readonly record struct GambleRewardSpec(
        byte Group,
        short Number,
        byte MinLevel,
        byte MaxLevel,
        bool AllowSkill,
        bool AllowLuck,
        bool AllowOption,
        bool AllowExcellent);

    private readonly record struct GambleTier(
        int Weight,
        int SkillChancePerMillion,
        int LuckChancePerMillion,
        int OptionChancePerMillion,
        int ExcellentChancePerMillion,
        IReadOnlyList<GambleRewardSpec> Items);
}
