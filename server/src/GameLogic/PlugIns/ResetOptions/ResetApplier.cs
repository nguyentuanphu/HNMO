// <copyright file="ResetApplier.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ResetOptions;

using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.CashShop;
using HNMO.Persistence;

/// <summary>
/// Puts a reset or relife plan into a character: the reset count, the level, the stats and the free points. The same
/// for a character in the game (its attribute system) and for one in the database (the website).
/// </summary>
public static class ResetApplier
{
    /// <summary>
    /// Whether an item of the bag counts for an item cost (equipped items never do).
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="cost">The cost.</param>
    /// <returns><c>true</c>, if it does.</returns>
    public static bool Matches(Item item, ResetItemCost cost) =>
        item.ItemSlot > InventoryConstants.LastEquippableItemSlotIndex
        && item.Definition is { } definition && definition.Group == cost.Group && definition.Number == cost.Number && item.Level >= cost.MinimumLevel;

    /// <summary>
    /// Counts the pieces of an item cost in a bag.
    /// </summary>
    /// <param name="items">The items of the bag.</param>
    /// <param name="cost">The cost.</param>
    /// <returns>The pieces.</returns>
    public static int Count(IEnumerable<Item> items, ResetItemCost cost) => items.Count(i => Matches(i, cost));

    /// <summary>
    /// Picks the items a plan takes, or <c>null</c> when some are missing.
    /// </summary>
    /// <param name="items">The items of the bag.</param>
    /// <param name="plan">The plan.</param>
    /// <returns>The items.</returns>
    public static List<Item>? PickItems(IEnumerable<Item> items, ResetPlan plan)
    {
        var bag = items.ToList();
        var picked = new List<Item>();
        foreach (var cost in plan.Items)
        {
            var found = bag.Where(i => Matches(i, cost) && !picked.Contains(i)).Take(cost.Count).ToList();
            if (found.Count < cost.Count)
            {
                return null;
            }

            picked.AddRange(found);
        }

        return picked;
    }

    /// <summary>
    /// Takes the coins of a plan from an account; nothing when one coin type is short.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <param name="plan">The plan.</param>
    /// <returns><c>true</c>, if they were taken.</returns>
    public static bool TryTakeCoins(Account account, ResetPlan plan)
    {
        if (plan.Coins.Any(c => account.GetCashShopCoins(c.CoinType) < c.Amount))
        {
            return false;
        }

        foreach (var coin in plan.Coins)
        {
            account.SetCashShopCoins(coin.CoinType, account.GetCashShopCoins(coin.CoinType) - coin.Amount);
        }

        return true;
    }

    /// <summary>
    /// The coins of an account by type.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <returns>The coins.</returns>
    public static Dictionary<CashShopCoinType, int> CoinsOf(Account? account) =>
        account is null
            ? new Dictionary<CashShopCoinType, int>()
            : Enum.GetValues<CashShopCoinType>().ToDictionary(t => t, account.GetCashShopCoins);

    /// <summary>
    /// Puts the reset count, level, stats and points of a plan into a character.
    /// </summary>
    /// <param name="character">The character (its class, experience and free points).</param>
    /// <param name="plan">The plan.</param>
    /// <param name="keepBuild">Whether cleared stats get the points back in the same shares.</param>
    /// <param name="get">Gets an attribute's value.</param>
    /// <param name="set">Sets an attribute's value.</param>
    public static void Apply(Character character, ResetPlan plan, bool keepBuild, Func<AttributeDefinition, float> get, Action<AttributeDefinition, float> set)
    {
        set(Stats.Resets, plan.ResetsAfter);
        set(Stats.Level, plan.LevelAfter);
        character.Experience = 0;
        var invested = new List<(StatAttributeDefinition Stat, int Points)>();
        if (plan.ClearStats)
        {
            foreach (var stat in IncreasableStats(character))
            {
                invested.Add((stat, Math.Max(0, (int)get(stat.Attribute!) - (int)stat.BaseValue)));
                set(stat.Attribute!, stat.BaseValue);
            }
        }

        switch (plan.PointsMode)
        {
            case ResetPointsMode.Total:
                character.LevelUpPoints = Math.Max(0, plan.Points);
                break;
            case ResetPointsMode.Add:
                character.LevelUpPoints = (int)Math.Min((long)character.LevelUpPoints + Math.Max(0, plan.Points), int.MaxValue);
                break;
        }

        var total = invested.Sum(i => (long)i.Points);
        if (!keepBuild || total <= 0 || character.LevelUpPoints <= 0)
        {
            return;
        }

        var free = character.LevelUpPoints;
        var used = 0;
        foreach (var (stat, points) in invested)
        {
            var share = (int)(free * (long)points / total);
            var maximum = stat.Attribute!.MaximumValue is { } max ? (int)max - (int)stat.BaseValue : int.MaxValue;
            share = Math.Clamp(share, 0, Math.Max(0, maximum));
            if (share > 0)
            {
                set(stat.Attribute!, stat.BaseValue + share);
                used += share;
            }
        }

        character.LevelUpPoints = free - used;
    }

    /// <summary>
    /// Clears the stats of a character: they go back to the class's start values and the points come back as free points.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <param name="get">Gets an attribute's value.</param>
    /// <param name="set">Sets an attribute's value.</param>
    /// <returns>The points which came back.</returns>
    public static int ClearStats(Character character, Func<AttributeDefinition, float> get, Action<AttributeDefinition, float> set)
    {
        var back = 0L;
        foreach (var stat in IncreasableStats(character))
        {
            back += Math.Max(0, (int)get(stat.Attribute!) - (int)stat.BaseValue);
            set(stat.Attribute!, stat.BaseValue);
        }

        character.LevelUpPoints = (int)Math.Min(character.LevelUpPoints + back, int.MaxValue);
        return (int)Math.Min(back, int.MaxValue);
    }

    /// <summary>
    /// Counts how many master skill points a character has invested across all learned master skills.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <returns>Total invested master points.</returns>
    public static int CountSpentMasterPoints(Character character) =>
        character.LearnedSkills
            .Where(s => s.Skill?.MasterDefinition is not null)
            .Sum(s => Math.Max(0, (int)s.Level));

    /// <summary>
    /// Clears all learned master skills of an offline character and refunds the points to
    /// <see cref="Character.MasterLevelUpPoints"/>.
    /// </summary>
    /// <param name="character">The offline character.</param>
    /// <param name="context">The persistence context used to delete removed skill rows.</param>
    /// <returns>Refunded master skill points.</returns>
    public static async ValueTask<int> ClearMasterSkillsOfflineAsync(Character character, IContext context)
    {
        var entries = character.LearnedSkills.Where(s => s.Skill?.MasterDefinition is not null).ToList();
        if (entries.Count == 0)
        {
            return 0;
        }

        var refunded = entries.Sum(e => Math.Max(0, (int)e.Level));
        foreach (var entry in entries)
        {
            character.LearnedSkills.Remove(entry);
            await context.DeleteAsync(entry).ConfigureAwait(false);
        }

        character.MasterLevelUpPoints = (int)Math.Min((long)character.MasterLevelUpPoints + refunded, int.MaxValue);
        return refunded;
    }

    /// <summary>
    /// The stats a player may raise (strength, agility, vitality, energy and, for the lord, command).
    /// </summary>
    /// <param name="character">The character.</param>
    /// <returns>The stats.</returns>
    public static IEnumerable<StatAttributeDefinition> IncreasableStats(Character character) =>
        character.CharacterClass?.StatAttributes.Where(s => s.IncreasableByPlayer && s.Attribute is not null) ?? Enumerable.Empty<StatAttributeDefinition>();

    /// <summary>
    /// The short key of a stat for the website ("str", "agi", "vit", "ene", "cmd").
    /// </summary>
    /// <param name="attribute">The stat.</param>
    /// <returns>The key; <c>null</c> for other attributes.</returns>
    public static string? StatKey(AttributeDefinition attribute) =>
        attribute.Id == Stats.BaseStrength.Id ? "str"
        : attribute.Id == Stats.BaseAgility.Id ? "agi"
        : attribute.Id == Stats.BaseVitality.Id ? "vit"
        : attribute.Id == Stats.BaseEnergy.Id ? "ene"
        : attribute.Id == Stats.BaseLeadership.Id ? "cmd"
        : null;
}
