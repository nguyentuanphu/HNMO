// <copyright file="ResetTier.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ResetOptions;

using HNMO.DataModel.Configuration;

/// <summary>
/// The rules of the resets from one reset count to another.
/// </summary>
public class ResetTier
{
    /// <summary>Gets or sets the lowest reset count (before the reset) this tier is for.</summary>
    [Range(0, 100_000)]
    public int FromResets { get; set; }

    /// <summary>Gets or sets the highest reset count (before the reset) this tier is for; -1 is no end.</summary>
    [Range(-1, 100_000)]
    public int ToResets { get; set; } = -1;

    /// <summary>Gets or sets the level needed.</summary>
    [Range(1, 2000)]
    public int RequiredLevel { get; set; } = 400;

    /// <summary>Gets or sets the level after the reset.</summary>
    [Range(1, 2000)]
    public int LevelAfterReset { get; set; } = 1;

    /// <summary>Gets or sets the zen it costs.</summary>
    [Range(0, 2_000_000_000)]
    public int Zen { get; set; }

    /// <summary>Gets or sets a value indicating whether the zen is multiplied by the reset number (reset 5 costs 5 × zen).</summary>
    public bool ZenTimesResetNumber { get; set; }

    /// <summary>Gets or sets the coins it costs.</summary>
    [Range(0, 1_000_000)]
    public int Coins { get; set; }

    /// <summary>Gets or sets the coin type of <see cref="Coins"/>.</summary>
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets or sets the stat points of one reset of this tier.</summary>
    [Range(0, 100_000)]
    public int Points { get; set; } = 500;

    /// <summary>Gets or sets what the reset does with the free points.</summary>
    public ResetPointsMode PointsMode { get; set; } = ResetPointsMode.Total;

    /// <summary>Gets or sets a value indicating whether the stats go back to the class's start values.</summary>
    public bool ClearStats { get; set; } = true;

    /// <summary>Gets or sets the resets a character may do per day (Sydney day); 0 is no limit.</summary>
    [Range(0, 1000)]
    public int DailyLimit { get; set; }

    /// <summary>Gets or sets the items it takes (none by default).</summary>
    public List<ResetItemCost> Items { get; set; } = new();

    /// <summary>
    /// Whether the tier is for a reset count (before the reset).
    /// </summary>
    /// <param name="resets">The reset count.</param>
    /// <returns><c>true</c>, if it is.</returns>
    public bool Covers(int resets) => resets >= this.FromResets && (this.ToResets < 0 || resets <= this.ToResets);
}
