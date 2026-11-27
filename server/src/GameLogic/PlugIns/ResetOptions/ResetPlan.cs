// <copyright file="ResetPlan.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ResetOptions;

using System.Globalization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// What a reset or relife needs and does.
/// </summary>
public sealed class ResetPlan
{
    /// <summary>Gets or sets the result of the check.</summary>
    public ResetResult Result { get; set; }

    /// <summary>Gets or sets a value indicating whether it's a relife.</summary>
    public bool IsRelife { get; set; }

    /// <summary>Gets or sets a value indicating whether the VIP advantages count.</summary>
    public bool Vip { get; set; }

    /// <summary>Gets or sets the level needed.</summary>
    public int RequiredLevel { get; set; }

    /// <summary>Gets or sets the level after.</summary>
    public int LevelAfter { get; set; } = 1;

    /// <summary>Gets or sets the resets after.</summary>
    public int ResetsAfter { get; set; }

    /// <summary>Gets or sets the relifes after.</summary>
    public int RelifesAfter { get; set; }

    /// <summary>Gets or sets the zen it costs.</summary>
    public long Zen { get; set; }

    /// <summary>Gets or sets the coins it costs.</summary>
    public List<ResetCoinCost> Coins { get; set; } = new();

    /// <summary>Gets or sets the items it takes.</summary>
    public List<ResetItemCost> Items { get; set; } = new();

    /// <summary>Gets or sets the first missing item, if any.</summary>
    public ResetItemCost? MissingItem { get; set; }

    /// <summary>Gets or sets what happens with the free points.</summary>
    public ResetPointsMode PointsMode { get; set; }

    /// <summary>Gets or sets the points: the new free points (<see cref="ResetPointsMode.Total"/>) or the points added (<see cref="ResetPointsMode.Add"/>).</summary>
    public int Points { get; set; }

    /// <summary>Gets or sets a value indicating whether the stats go back to the start values.</summary>
    public bool ClearStats { get; set; }

    /// <summary>Gets or sets the resets (or relifes) done today.</summary>
    public int Today { get; set; }

    /// <summary>Gets or sets the resets (or relifes) allowed today; 0 is no limit.</summary>
    public int DailyLimit { get; set; }

    /// <summary>Gets or sets the delegated resets done today.</summary>
    public int TodayDelegated { get; set; }

    /// <summary>Gets or sets the delegated resets allowed today; 0 is only the tier's limit.</summary>
    public int DelegatedDailyLimit { get; set; }

    /// <summary>Gets or sets the most resets (or relifes); 0 is no limit.</summary>
    public int Maximum { get; set; }

    /// <summary>Gets or sets the relife resets needed (only for a relife).</summary>
    public int ResetsNeeded { get; set; }
}
