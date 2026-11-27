// <copyright file="EconomyAnomaly.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Economy;

/// <summary>
/// Sudden jump anomaly detected when comparing two consecutive daily economy snapshots.
/// </summary>
public sealed class EconomyAnomaly
{
    /// <summary>Gets or sets Sydney calendar date (<c>yyyy-MM-dd</c>) when the jump was detected.</summary>
    public string DateSydney { get; set; } = string.Empty;

    /// <summary>Gets or sets scope (<c>Global</c> or <c>Account</c>).</summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>Gets or sets account login when <see cref="Scope"/> is <c>Account</c>.</summary>
    public string AccountLogin { get; set; } = string.Empty;

    /// <summary>Gets or sets affected currency or metric (<c>Zen</c>, <c>Coins</c>, or <c>Jewels</c>).</summary>
    public string Metric { get; set; } = string.Empty;

    /// <summary>Gets or sets previous day's total.</summary>
    public long PreviousValue { get; set; }

    /// <summary>Gets or sets current day's total.</summary>
    public long NewValue { get; set; }

    /// <summary>Gets or sets percentage increase vs the previous value.</summary>
    public double PercentIncrease { get; set; }
}
