// <copyright file="OnlineTimeCoinsConfiguration.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Missions;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;

/// <summary>
/// Coins per hour in the game.
/// </summary>
public class OnlineTimeCoinsConfiguration
{
    /// <summary>Gets or sets a value indicating whether the source gives coins.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the coin type.</summary>
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets or sets the coins per full hour in the game.</summary>
    [Range(0, 100_000)]
    public int CoinsPerHour { get; set; } = 5;

    /// <summary>Gets or sets the most coins of this source per account and day.</summary>
    [Range(0, 1_000_000)]
    public int DailyCap { get; set; } = 40;

    /// <summary>Gets or sets a value indicating whether time with auto play running counts half.</summary>
    public bool AutoPlayCountsHalf { get; set; } = true;
}
