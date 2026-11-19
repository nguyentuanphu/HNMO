// <copyright file="CommunityCoinReward.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Community;

using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// A one-time coin reward (linking Discord, joining the Zalo group).
/// </summary>
public class CommunityCoinReward
{
    /// <summary>Gets or sets a value indicating whether the reward is given.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the coins, once per account.</summary>
    [Range(0, 1_000_000)]
    public int Coins { get; set; } = 50;

    /// <summary>Gets or sets the coin type.</summary>
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.GoblinPoints;
}
