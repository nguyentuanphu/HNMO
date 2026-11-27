// <copyright file="RelifeOptions.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ResetOptions;

using HNMO.DataModel.Configuration;

/// <summary>
/// The relife (grand reset): after enough resets, the resets go back to 0 (or keep a share) for permanent bonus points.
/// </summary>
public class RelifeOptions
{
    /// <summary>Gets or sets a value indicating whether relifes can be done.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the resets needed.</summary>
    [Range(1, 100_000)]
    public int ResetsNeeded { get; set; } = 50;

    /// <summary>Gets or sets the level needed.</summary>
    [Range(1, 2000)]
    public int RequiredLevel { get; set; } = 400;

    /// <summary>Gets or sets the level after the relife.</summary>
    [Range(1, 2000)]
    public int LevelAfterRelife { get; set; } = 1;

    /// <summary>Gets or sets the zen it costs.</summary>
    [Range(0, 2_000_000_000)]
    public int Zen { get; set; } = 50_000_000;

    /// <summary>Gets or sets the coins it costs.</summary>
    [Range(0, 1_000_000)]
    public int Coins { get; set; }

    /// <summary>Gets or sets the coin type of <see cref="Coins"/>.</summary>
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets or sets the share of the resets kept in percent (0: back to 0).</summary>
    [Range(0, 100)]
    public int KeepResetsPercent { get; set; }

    /// <summary>
    /// Gets or sets the permanent bonus points of each relife: they are part of every later point total (a character
    /// with 3 relifes has 3 × this on top of its resets' points).
    /// </summary>
    [Range(0, 1_000_000)]
    public int PointsPerRelife { get; set; } = 2000;

    /// <summary>Gets or sets the most relifes of a character; 0 is no limit.</summary>
    [Range(0, 10_000)]
    public int MaximumRelifes { get; set; } = 10;

    /// <summary>Gets or sets the relifes a character may do per day; 0 is no limit.</summary>
    [Range(0, 100)]
    public int DailyLimit { get; set; } = 1;

    /// <summary>Gets or sets the items it takes.</summary>
    public List<ResetItemCost> Items { get; set; } = new();
}
