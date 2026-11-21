// <copyright file="GuildLadderReward.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Guilds;

using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// The reward of ranks of the weekly guild ladder, for every member of a guild.
/// </summary>
public class GuildLadderReward
{
    /// <summary>Gets or sets the first rank.</summary>
    [Range(1, 100)]
    public int RankFrom { get; set; } = 1;

    /// <summary>Gets or sets the last rank.</summary>
    [Range(1, 100)]
    public int RankTo { get; set; } = 1;

    /// <summary>Gets or sets the coins for each member.</summary>
    [Range(0, 1_000_000)]
    public int CoinsPerMember { get; set; }

    /// <summary>Gets or sets the extra coins for the guild master.</summary>
    [Range(0, 1_000_000)]
    public int MasterBonusCoins { get; set; }

    /// <summary>Gets or sets the coin type.</summary>
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets or sets the zen for each member.</summary>
    [Range(0, 2_000_000_000)]
    public int ZenPerMember { get; set; }

    /// <summary>Gets or sets the items for each member.</summary>
    public ICollection<MissionItemReward> Items { get; set; } = new List<MissionItemReward>();
}
