// <copyright file="WeeklyRankingConfiguration.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.WorldBosses;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// A weekly ranking (Monday to Sunday) whose top 10 get coins when the week ends.
/// </summary>
public class WeeklyRankingConfiguration
{
    /// <summary>Gets or sets a value indicating whether the ranking pays coins.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the coin type.</summary>
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets or sets the coins of the ranks.</summary>
    public ICollection<RankCoins> Ranks { get; set; } = DefaultRanks(200, 120, 80, 30);

    /// <summary>Gets or sets the points of a character who was in the event when it ended (events only).</summary>
    [Range(0, 10_000)]
    public int PointsFinish { get; set; } = 1;

    /// <summary>Gets or sets the points of the event's winner (events only).</summary>
    [Range(0, 10_000)]
    public int PointsWin { get; set; } = 3;

    /// <summary>
    /// Creates ranks 1, 2 and 3 with their coins and the ranks 4 to 10 with the last amount.
    /// </summary>
    /// <param name="first">Rank 1.</param>
    /// <param name="second">Rank 2.</param>
    /// <param name="third">Rank 3.</param>
    /// <param name="rest">Ranks 4 to 10.</param>
    /// <returns>The ranks.</returns>
    public static List<RankCoins> DefaultRanks(int first, int second, int third, int rest) =>
        Enumerable.Range(1, 10).Select(r => new RankCoins { Rank = r, Coins = r switch { 1 => first, 2 => second, 3 => third, _ => rest } }).ToList();

    /// <summary>Gets the coins of a rank.</summary>
    /// <param name="rank">The rank.</param>
    /// <returns>The coins.</returns>
    public int CoinsOf(int rank) => this.Ranks.LastOrDefault(r => r.Rank == rank)?.Coins ?? 0;
}
