// <copyright file="PotionShopStorageBuilder.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.Persistence.Initialization.Items;

/// <summary>
/// Builds the 32-item potion, ammunition, Town Portal Scroll, and Armor of Guardsman merchant inventory
/// matching the Season 6 and Season 9 server suites (with Mercenary Contract slots 30-31 omitted).
/// </summary>
internal static class PotionShopStorageBuilder
{
    private static readonly (byte Slot, byte Group, byte Number, byte Level, byte Durability)[] ShopEntries =
    [
        (0, 14, 0, 0, 1),   // Apple +0 x1
        (8, 14, 0, 0, 3),   // Apple +0 x3
        (16, 14, 0, 1, 1),  // Apple +1 x1
        (20, 14, 0, 1, 3),  // Apple +1 x3
        (1, 14, 1, 0, 1),   // Small Healing Potion +0 x1
        (9, 14, 1, 0, 3),   // Small Healing Potion +0 x3
        (17, 14, 1, 1, 1),  // Small Healing Potion +1 x1
        (21, 14, 1, 1, 3),  // Small Healing Potion +1 x3
        (2, 14, 2, 0, 1),   // Medium Healing Potion +0 x1
        (10, 14, 2, 0, 3),  // Medium Healing Potion +0 x3
        (18, 14, 2, 1, 1),  // Medium Healing Potion +1 x1
        (22, 14, 2, 1, 3),  // Medium Healing Potion +1 x3
        (3, 14, 3, 0, 1),   // Large Healing Potion +0 x1
        (11, 14, 3, 0, 3),  // Large Healing Potion +0 x3
        (19, 14, 3, 1, 1),  // Large Healing Potion +1 x1
        (23, 14, 3, 1, 3),  // Large Healing Potion +1 x3
        (4, 14, 4, 0, 1),   // Small Mana Potion +0 x1
        (12, 14, 4, 0, 3),  // Small Mana Potion +0 x3
        (5, 14, 5, 0, 1),   // Medium Mana Potion +0 x1
        (13, 14, 5, 0, 3),  // Medium Mana Potion +0 x3
        (6, 14, 6, 0, 1),   // Large Mana Potion +0 x1
        (14, 14, 6, 0, 3),  // Large Mana Potion +0 x3
        (7, 14, 8, 0, 1),   // Antidote +0 x1
        (15, 14, 8, 0, 3),  // Antidote +0 x3
        (24, 4, 7, 0, 255), // Bolt +0
        (25, 4, 7, 1, 255), // Bolt +1
        (26, 4, 7, 2, 255), // Bolt +2
        (27, 4, 15, 0, 255), // Arrow +0
        (28, 4, 15, 1, 255), // Arrow +1
        (29, 4, 15, 2, 255), // Arrow +2
        (32, 14, 10, 0, 1), // Town Portal Scroll
        (33, 13, 29, 0, 1), // Armor of Guardsman
    ];

    /// <summary>Creates a fresh 32-item merchant storage with a deterministic GUID for <paramref name="npcNumber"/>.</summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    /// <param name="npcNumber">The merchant NPC number.</param>
    /// <returns>The initialized <see cref="ItemStorage"/>.</returns>
    public static ItemStorage Build(IContext context, GameConfiguration gameConfiguration, short npcNumber)
    {
        var storage = context.CreateNew<ItemStorage>();
        storage.SetGuid(npcNumber);
        var helper = new ItemHelper(context, gameConfiguration);
        foreach (var (slot, group, number, level, durability) in ShopEntries)
        {
            if (!gameConfiguration.Items.Any(def => def.Group == group && def.Number == number))
            {
                continue;
            }

            var item = helper.CreateItem(slot, number, group, durability, level);
            storage.Items.Add(item);
        }

        return storage;
    }
}
