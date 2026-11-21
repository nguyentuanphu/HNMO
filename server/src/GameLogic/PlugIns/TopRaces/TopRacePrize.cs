// <copyright file="TopRacePrize.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TopRaces;

using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// The prize of a range of ranks.
/// </summary>
public class TopRacePrize
{
    /// <summary>Gets or sets the first rank.</summary>
    [Range(1, 100)]
    public int RankFrom { get; set; } = 1;

    /// <summary>Gets or sets the last rank.</summary>
    [Range(1, 100)]
    public int RankTo { get; set; } = 1;

    /// <summary>Gets or sets the coins (to the account; for a guild race, to each member's account).</summary>
    [Range(0, 1_000_000)]
    public int Coins { get; set; }

    /// <summary>Gets or sets the coin type.</summary>
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets or sets the zen (to the character; for a guild race, to each member).</summary>
    [Range(0, 2_000_000_000)]
    public int Zen { get; set; }

    /// <summary>Gets or sets the items.</summary>
    public ICollection<MissionItemReward> Items { get; set; } = new List<MissionItemReward>();

    /// <summary>Gets a value indicating whether the prize gives nothing.</summary>
    [JsonIgnore]
    public bool IsEmpty => this.Coins <= 0 && this.Zen <= 0 && this.Items.All(i => i.Count <= 0);
}
