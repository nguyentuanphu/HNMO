// <copyright file="WebsiteResetOptions.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ResetOptions;

using HNMO.DataModel.Configuration;

/// <summary>
/// What the website's account page may do.
/// </summary>
public class WebsiteResetOptions
{
    /// <summary>Gets or sets a value indicating whether characters can be reset on the website (only while the account is offline).</summary>
    public bool AllowReset { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether relifes can be done on the website.</summary>
    public bool AllowRelife { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether free points can be added to the stats on the website.</summary>
    public bool AllowAddStats { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the stats can be cleared (the points come back) on the website.</summary>
    public bool AllowClearStats { get; set; } = true;

    /// <summary>Gets or sets the zen clearing the stats costs.</summary>
    [Range(0, 2_000_000_000)]
    public int ClearStatsZen { get; set; } = 1_000_000;

    /// <summary>Gets or sets the coins clearing the stats costs.</summary>
    [Range(0, 1_000_000)]
    public int ClearStatsCoins { get; set; }

    /// <summary>Gets or sets the coin type of <see cref="ClearStatsCoins"/>.</summary>
    public CashShopCoinType ClearStatsCoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets or sets a value indicating whether master skill points can be cleared and refunded on the website.</summary>
    public bool AllowClearMasterSkills { get; set; } = true;

    /// <summary>Gets or sets the zen clearing master skill points costs on the website.</summary>
    [Range(0, 2_000_000_000)]
    public int ClearMasterSkillsZen { get; set; } = 5_000_000;

    /// <summary>Gets or sets the coins clearing master skill points costs on the website.</summary>
    [Range(0, 1_000_000)]
    public int ClearMasterSkillsCoins { get; set; }

    /// <summary>Gets or sets the coin type of <see cref="ClearMasterSkillsCoins"/>.</summary>
    public CashShopCoinType ClearMasterSkillsCoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets or sets a value indicating whether the delegated reset can be switched on and off on the website.</summary>
    public bool AllowDelegatedSwitch { get; set; } = true;
}
