// <copyright file="FixedItemOptionsEngine.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.FixedItemOptions;

using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;

/// <summary>
/// Pure rule matching and option-pinning engine for <see cref="FixedItemOptionsPlugIn"/>.
/// </summary>
public static class FixedItemOptionsEngine
{
    /// <summary>Normalizes <paramref name="sourceFilter"/> into <c>"all"</c>, <c>"drop"</c>, or <c>"box_event"</c>.</summary>
    /// <param name="sourceFilter">Raw source filter text.</param>
    /// <returns>Normalized source filter string.</returns>
    public static string NormalizeSourceFilter(string? sourceFilter)
    {
        var clean = (sourceFilter ?? string.Empty).Trim().ToLowerInvariant();
        return clean switch
        {
            "drop" or "monster" or "drops" => "drop",
            "box_event" or "box" or "event" or "reward" => "box_event",
            _ => "all",
        };
    }

    /// <summary>Returns <c>true</c> if <paramref name="rule"/> matches <paramref name="item"/>, <paramref name="source"/>, and <paramref name="mapNumber"/>.</summary>
    /// <param name="rule">The rule to evaluate.</param>
    /// <param name="item">The candidate item.</param>
    /// <param name="source">Origin source (<c>"drop"</c> or <c>"box_event"</c>).</param>
    /// <param name="mapNumber">Optional current map number.</param>
    /// <returns><c>true</c> if the rule applies.</returns>
    public static bool Matches(FixedItemOptionRule rule, Item item, string source, short? mapNumber)
    {
        if (!rule.Enabled || item.Definition is not { } def)
        {
            return false;
        }

        if (rule.ItemGroup >= 0 && def.Group != rule.ItemGroup)
        {
            return false;
        }

        if (rule.ItemNumber >= 0 && def.Number != rule.ItemNumber)
        {
            return false;
        }

        var filter = NormalizeSourceFilter(rule.SourceFilter);
        var cleanSource = NormalizeSourceFilter(source);
        if (!string.Equals(filter, "all", StringComparison.Ordinal) && !string.Equals(filter, cleanSource, StringComparison.Ordinal))
        {
            return false;
        }

        if (rule.MapNumber >= 0 && mapNumber != rule.MapNumber)
        {
            return false;
        }

        return true;
    }

    /// <summary>Applies all matching enabled rules in <paramref name="config"/> to <paramref name="item"/>.</summary>
    /// <param name="item">Target item to mutate.</param>
    /// <param name="config">Plug-in configuration.</param>
    /// <param name="plugInActive">Whether the plug-in is currently enabled.</param>
    /// <param name="source">Origin source (<c>"drop"</c> or <c>"box_event"</c>).</param>
    /// <param name="mapNumber">Optional current map number.</param>
    /// <param name="randomizer">Optional randomizer used when filling non-bitmask minimum excellent options.</param>
    /// <returns>Number of rules matched and applied.</returns>
    public static int ApplyRules(
        Item item,
        FixedItemOptionsConfiguration config,
        bool plugInActive,
        string source,
        short? mapNumber,
        IRandomizer? randomizer = null)
    {
        if (!plugInActive || !config.Enabled || item.Definition is null)
        {
            return 0;
        }

        var matched = 0;
        foreach (var rule in config.Rules ?? [])
        {
            if (!Matches(rule, item, source, mapNumber))
            {
                continue;
            }

            ApplySingleRule(item, rule, randomizer);
            matched++;
        }

        return matched;
    }

    private static void ApplySingleRule(Item item, FixedItemOptionRule rule, IRandomizer? randomizer)
    {
        var def = item.Definition!;
        if (rule.MinLevel > item.Level)
        {
            item.Level = Math.Min(rule.MinLevel, def.MaximumItemLevel);
        }

        if (rule.PinSkill && def.Skill is not null)
        {
            item.HasSkill = true;
        }

        if (rule.PinLuck)
        {
            EnsureLuckOption(item, def);
        }

        if (rule.MinOptionLevel > 0)
        {
            EnsureMinimumNormalOption(item, def, Math.Clamp((int)rule.MinOptionLevel, 1, 7));
        }

        if (rule.ForcedExcellentBits > 0)
        {
            EnsureForcedExcellentBits(item, def, rule.ForcedExcellentBits);
        }

        if (rule.MinExcellentCount > 0)
        {
            EnsureMinimumExcellentCount(item, def, Math.Clamp(rule.MinExcellentCount, 1, 6), randomizer);
        }
    }

    private static void EnsureLuckOption(Item item, ItemDefinition def)
    {
        if (item.ItemOptions.Any(o => Equals(o.ItemOption?.OptionType, ItemOptionTypes.Luck)))
        {
            return;
        }

        var luckOption = (def.PossibleItemOptions ?? [])
            .SelectMany(o => o.PossibleOptions ?? [])
            .FirstOrDefault(o => Equals(o.OptionType, ItemOptionTypes.Luck));
        if (luckOption is not null)
        {
            item.ItemOptions.Add(new ItemOptionLink { ItemOption = luckOption });
        }
    }

    private static void EnsureMinimumNormalOption(Item item, ItemDefinition def, int minLevel)
    {
        var existing = item.ItemOptions.FirstOrDefault(o => Equals(o.ItemOption?.OptionType, ItemOptionTypes.Option));
        if (existing is not null)
        {
            if (existing.Level < minLevel)
            {
                existing.Level = minLevel;
            }

            return;
        }

        var possible = (def.PossibleItemOptions ?? [])
            .SelectMany(o => o.PossibleOptions ?? [])
            .Where(o => Equals(o.OptionType, ItemOptionTypes.Option) && !o.IsFixedOption())
            .FirstOrDefault();
        if (possible is null)
        {
            return;
        }

        item.ItemOptions.Add(new ItemOptionLink
        {
            ItemOption = possible,
            Level = minLevel,
        });
    }

    private static void EnsureForcedExcellentBits(Item item, ItemDefinition def, int bitmask)
    {
        var possible = (def.PossibleItemOptions ?? [])
            .SelectMany(o => o.PossibleOptions ?? [])
            .Where(o => Equals(o.OptionType, ItemOptionTypes.Excellent))
            .ToList();
        foreach (var opt in possible)
        {
            if (opt.Number <= 0 || (bitmask & opt.Number) == 0)
            {
                continue;
            }

            if (item.ItemOptions.All(link => !SameOption(link.ItemOption, opt)))
            {
                item.ItemOptions.Add(new ItemOptionLink { ItemOption = opt });
            }
        }
    }

    private static bool SameOption(IncreasableItemOption? a, IncreasableItemOption? b)
    {
        if (a is null || b is null)
        {
            return false;
        }

        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (!Equals(a.OptionType, b.OptionType))
        {
            return false;
        }

        return a.Number == b.Number;
    }

    private static void EnsureMinimumExcellentCount(Item item, ItemDefinition def, int targetCount, IRandomizer? randomizer)
    {
        var possible = (def.PossibleItemOptions ?? [])
            .SelectMany(o => o.PossibleOptions ?? [])
            .Where(o => Equals(o.OptionType, ItemOptionTypes.Excellent))
            .ToList();
        if (possible.Count == 0)
        {
            return;
        }

        var current = item.ItemOptions.Count(o => Equals(o.ItemOption?.OptionType, ItemOptionTypes.Excellent));
        while (current < targetCount)
        {
            var remaining = possible.Where(p => item.ItemOptions.All(link => !SameOption(link.ItemOption, p))).ToList();
            if (remaining.Count == 0)
            {
                break;
            }

            var chosen = randomizer is not null
                ? remaining[randomizer.NextInt(0, remaining.Count)]
                : remaining[0];
            item.ItemOptions.Add(new ItemOptionLink { ItemOption = chosen });
            current++;
        }
    }
}
