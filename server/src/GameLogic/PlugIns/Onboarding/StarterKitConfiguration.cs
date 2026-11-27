// <copyright file="StarterKitConfiguration.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Onboarding;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// The starter kit, given once to a new character (or account) at its first login; each piece can be switched off.
/// </summary>
public class StarterKitConfiguration
{
    /// <summary>Gets or sets a value indicating whether the starter kit is given.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether it's given once per account (to its first new character) instead of once per character.</summary>
    public bool PerAccount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the zen and the coins go only to the account's first new character, while
    /// the items and the buff go to every new character (owner's choice, 2026-11-21: coins kept on the account could
    /// otherwise be collected again and again by creating and deleting characters).
    /// </summary>
    public bool MoneyPerAccount { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the items are given.</summary>
    public bool ItemsEnabled { get; set; } = true;

    /// <summary>Gets or sets the items; an item with a class number only goes to that class.</summary>
    public ICollection<StarterKitItem> Items { get; set; } = CreateDefaultItems();

    /// <summary>Gets or sets a value indicating whether the zen is given.</summary>
    public bool ZenEnabled { get; set; } = true;

    /// <summary>Gets or sets the zen.</summary>
    [Range(0, 2_000_000_000)]
    public int Zen { get; set; } = 50_000;

    /// <summary>Gets or sets a value indicating whether the coins are given.</summary>
    public bool CoinsEnabled { get; set; } = true;

    /// <summary>Gets or sets the coins.</summary>
    [Range(0, 1_000_000)]
    public int Coins { get; set; } = 20;

    /// <summary>Gets or sets the coin type of <see cref="Coins"/>.</summary>
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets or sets a value indicating whether extra stat points are given (when the character is created).</summary>
    public bool PointsEnabled { get; set; }

    /// <summary>Gets or sets the extra stat points (a few levels' worth).</summary>
    [Range(0, 10_000)]
    public int LevelUpPoints { get; set; } = 15;

    /// <summary>Gets or sets the temporary EXP buff.</summary>
    public StarterKitBuffConfiguration ExperienceBuff { get; set; } = new();

    /// <summary>
    /// Creates the default items: potions and a town portal scroll for everyone, a first weapon for the
    /// three classic classes.
    /// </summary>
    /// <returns>The items.</returns>
    public static List<StarterKitItem> CreateDefaultItems() =>
    [
        new() { Group = 14, Number = 1, Count = 3, Durability = 3 },
        new() { Group = 14, Number = 4, Count = 2, Durability = 3 },
        new() { Group = 14, Number = 10, Count = 1 },
        new() { Group = 5, Number = 0, Level = 3, Skill = false, Luck = true, ClassNumber = 0 },
        new() { Group = 0, Number = 1, Level = 3, Skill = true, Luck = true, ClassNumber = 4 },
        new() { Group = 4, Number = 0, Level = 3, Skill = true, Luck = true, ClassNumber = 8 },
        new() { Group = 4, Number = 15, Count = 1, Durability = 255, ClassNumber = 8 },
    ];
}
