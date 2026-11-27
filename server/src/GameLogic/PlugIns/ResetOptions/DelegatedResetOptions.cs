// <copyright file="DelegatedResetOptions.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ResetOptions;

using HNMO.DataModel.Configuration;

/// <summary>
/// The delegated reset ("ủy thác reset"): while a character is offline leveling, the server resets it when it reaches
/// the level needed, within the daily limits and paying the costs, and it goes on leveling.
/// </summary>
public class DelegatedResetOptions
{
    /// <summary>Gets or sets a value indicating whether players can switch it on.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the zen of each delegated reset on top of the tier's costs.</summary>
    [Range(0, 2_000_000_000)]
    public int ExtraZen { get; set; }

    /// <summary>Gets or sets the coins of each delegated reset on top of the tier's costs.</summary>
    [Range(0, 1_000_000)]
    public int ExtraCoins { get; set; }

    /// <summary>Gets or sets the coin type of <see cref="ExtraCoins"/>.</summary>
    public CashShopCoinType ExtraCoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets or sets the delegated resets per character and day; 0 is only the tier's limit.</summary>
    [Range(0, 1000)]
    public int DailyLimit { get; set; }

    /// <summary>Gets or sets a value indicating whether a reset which clears the stats puts the points back in the same shares (the character keeps its build and goes on hunting).</summary>
    public bool KeepBuild { get; set; } = true;

    /// <summary>Gets or sets how often the server looks at the offline characters, in seconds.</summary>
    [Range(5, 3600)]
    public int CheckSeconds { get; set; } = 30;
}
