// <copyright file="MilestoneCoinsConfiguration.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Missions;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;

/// <summary>
/// One-time coins per character at level milestones and at each reset.
/// </summary>
public class MilestoneCoinsConfiguration
{
    /// <summary>Gets or sets a value indicating whether the source gives coins.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the coin type.</summary>
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets or sets the levels and their coins.</summary>
    public ICollection<LevelCoins> Levels { get; set; } = new List<LevelCoins>
    {
        new() { Level = 100, Coins = 20 },
        new() { Level = 200, Coins = 30 },
        new() { Level = 300, Coins = 50 },
        new() { Level = 400, Coins = 80 },
    };

    /// <summary>Gets or sets the coins for each reset.</summary>
    [Range(0, 1_000_000)]
    public int CoinsPerReset { get; set; } = 50;
}
