// <copyright file="MissionReward.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Missions;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;

/// <summary>
/// A reward: zen, items and coins.
/// </summary>
public class MissionReward
{
    /// <summary>Gets or sets the zen.</summary>
    [Range(0, 2_000_000_000)]
    public int Zen { get; set; }

    /// <summary>Gets or sets the items.</summary>
    public ICollection<MissionItemReward> Items { get; set; } = new List<MissionItemReward>();

    /// <summary>Gets or sets the coins.</summary>
    [Range(0, 1_000_000)]
    public int Coins { get; set; }

    /// <summary>Gets or sets the coin type of <see cref="Coins"/>.</summary>
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets a value indicating whether the reward gives nothing.</summary>
    [JsonIgnore]
    public bool IsEmpty => this.Zen <= 0 && this.Coins <= 0 && this.Items.All(i => i.Count <= 0);
}
