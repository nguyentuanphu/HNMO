// <copyright file="TalismanEngine.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Talismans;

/// <summary>
/// Pure evaluation engine for carried bag talismans/figurines and capped stat-point consumables.
/// </summary>
public static class TalismanEngine
{
    /// <summary>
    /// Computes combined passive bonuses and granted magic effects for items carried in regular inventory slots.
    /// </summary>
    /// <param name="inventoryItems">All items in the character's inventory storage.</param>
    /// <param name="config">The active <see cref="TalismanConfiguration"/>.</param>
    /// <returns>The aggregated bag summary.</returns>
    public static TalismanBagSummary EvaluateBagBonuses(IEnumerable<Item> inventoryItems, TalismanConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(inventoryItems);
        ArgumentNullException.ThrowIfNull(config);

        var rules = config.Talismans ?? [];
        if (rules.Count == 0)
        {
            return new TalismanBagSummary(0, 0, 0, 0, 0, 0, 0, 0, []);
        }

        var carriedItems = inventoryItems
            .Where(i => i.Definition is not null
                     && i.ItemSlot >= InventoryConstants.EquippableSlotsCount
                     && i.ItemSlot < InventoryConstants.FirstStoreItemSlotIndex)
            .ToList();

        var activeCount = 0;
        float expPct = 0;
        float zenPct = 0;
        var dmg = 0;
        var def = 0;
        var hp = 0;
        var spd = 0;
        float critPct = 0;
        var effectIds = new List<short>();

        foreach (var rule in rules)
        {
            var matches = carriedItems.Where(i =>
                i.Definition!.Group == rule.ItemGroup
                && i.Definition.Number == rule.ItemNumber
                && (rule.MinItemLevel is null || i.Level >= rule.MinItemLevel.Value)).ToList();

            if (matches.Count == 0)
            {
                continue;
            }

            var stacks = rule.StackCopies
                ? Math.Min(matches.Count, Math.Clamp(rule.MaxStacks, 1, 64))
                : 1;

            activeCount += stacks;
            expPct += Math.Max(0, rule.BonusExpPercent) * stacks;
            zenPct += Math.Max(0, rule.BonusZenPercent) * stacks;
            dmg += Math.Max(0, rule.BonusDamage) * stacks;
            def += Math.Max(0, rule.BonusDefense) * stacks;
            hp += Math.Max(0, rule.BonusMaxHp) * stacks;
            spd += Math.Max(0, rule.BonusAttackSpeed) * stacks;
            critPct += Math.Max(0, rule.CriticalChancePercent) * stacks;

            if (rule.GrantedMagicEffectNumber is > 0 and var eff && !effectIds.Contains(eff))
            {
                effectIds.Add(eff);
            }
        }

        return new TalismanBagSummary(activeCount, expPct, zenPct, dmg, def, hp, spd, critPct, effectIds);
    }

    /// <summary>
    /// Evaluates whether <paramref name="item"/> is a configured stat consumable and how many points can be granted without exceeding caps.
    /// </summary>
    /// <param name="config">The active <see cref="TalismanConfiguration"/>.</param>
    /// <param name="item">The consumed item.</param>
    /// <param name="currentClaimed">Extra stat points already claimed by this character.</param>
    /// <param name="allowsCommand">Whether the character class supports Command.</param>
    /// <returns>The <see cref="StatConsumeDecision"/>.</returns>
    public static StatConsumeDecision EvaluateStatConsume(
        TalismanConfiguration config,
        Item item,
        int currentClaimed,
        bool allowsCommand)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(item);

        if (item.Definition is null)
        {
            return default;
        }

        var rule = (config.StatConsumables ?? []).FirstOrDefault(r =>
            r.ItemGroup == item.Definition.Group && r.ItemNumber == item.Definition.Number);
        if (rule is null)
        {
            return default;
        }

        var bucket = NormalizeBucket(rule.TargetBucket);
        var nameEn = string.IsNullOrWhiteSpace(rule.NameEn) ? item.Definition.Name.ToString() ?? "Stat Scroll" : rule.NameEn;
        var nameVi = string.IsNullOrWhiteSpace(rule.NameVi) ? nameEn : rule.NameVi;
        var effectiveCap = Math.Max(1, rule.MaxCapOverride is > 0 ? rule.MaxCapOverride.Value : config.GlobalStatPointsCap);

        if (item.Durability <= 0)
        {
            return new StatConsumeDecision(true, false, "empty", 0, effectiveCap, bucket, nameEn, nameVi);
        }

        if (bucket == "cmd" && !allowsCommand)
        {
            return new StatConsumeDecision(true, false, "no_command", 0, effectiveCap, bucket, nameEn, nameVi);
        }

        var remaining = Math.Max(0, effectiveCap - Math.Max(0, currentClaimed));
        if (remaining <= 0)
        {
            return new StatConsumeDecision(true, false, "cap_reached", 0, effectiveCap, bucket, nameEn, nameVi);
        }

        var granted = Math.Min(Math.Max(1, rule.PointsPerUse), remaining);
        return new StatConsumeDecision(true, true, null, granted, effectiveCap, bucket, nameEn, nameVi);
    }

    /// <summary>
    /// Normalizes a configured target stat bucket string.
    /// </summary>
    /// <param name="bucket">Configured bucket string.</param>
    /// <returns>Normalized lowercase bucket key.</returns>
    public static string NormalizeBucket(string? bucket) =>
        (bucket ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "str" or "strength" => "str",
            "agi" or "agility" => "agi",
            "vit" or "vitality" => "vit",
            "ene" or "energy" => "ene",
            "cmd" or "command" or "leadership" => "cmd",
            _ => "levelUpPoints",
        };
}
