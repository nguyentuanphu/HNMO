// <copyright file="ResetVipOptions.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ResetOptions;

using HNMO.DataModel.Configuration;

/// <summary>
/// The advantages of VIP accounts (the account's "Is VIP" value, from its attributes or a grant until a date).
/// </summary>
public class ResetVipOptions
{
    /// <summary>Gets or sets a value indicating whether VIP accounts get the advantages.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the lowest VIP value which counts.</summary>
    [Range(1, 100)]
    public int MinimumVipLevel { get; set; } = 1;

    /// <summary>Gets or sets how many levels lower a VIP resets.</summary>
    [Range(0, 1000)]
    public int LevelReduction { get; set; } = 20;

    /// <summary>Gets or sets the discount on the zen in percent.</summary>
    [Range(0, 100)]
    public int ZenDiscountPercent { get; set; } = 20;

    /// <summary>Gets or sets the discount on the coins in percent.</summary>
    [Range(0, 100)]
    public int CoinsDiscountPercent { get; set; } = 20;

    /// <summary>Gets or sets the extra resets per day (only where the tier has a limit).</summary>
    [Range(0, 1000)]
    public int ExtraDailyResets { get; set; } = 2;

    /// <summary>Gets or sets the extra stat points in percent.</summary>
    [Range(0, 1000)]
    public int ExtraPointsPercent { get; set; }

    /// <summary>Gets or sets a value indicating whether the relife gets the level reduction and the discounts too.</summary>
    public bool AppliesToRelife { get; set; } = true;
}
