// <copyright file="EconomyConfiguration.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Economy;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Configuration for <see cref="EconomyDashboardPlugIn"/> (ROADMAP 14.25).
/// </summary>
public sealed class EconomyConfiguration
{
    /// <summary>Gets or sets a value indicating whether daily economy snapshots are enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets percentage jump over the previous day required to flag an anomaly.</summary>
    [Range(5, 10000)]
    public int AnomalyJumpPercentThreshold { get; set; } = 50;

    /// <summary>Gets or sets minimum zen increase required before a zen jump is flagged.</summary>
    [Range(1, 2000000000)]
    public long AnomalyMinZenDelta { get; set; } = 10_000_000;

    /// <summary>Gets or sets minimum jewel count increase required before a jewel jump is flagged.</summary>
    [Range(1, 1000000)]
    public int AnomalyMinJewelDelta { get; set; } = 25;

    /// <summary>Gets or sets minimum coin increase required before a coin jump is flagged.</summary>
    [Range(1, 10000000)]
    public int AnomalyMinCoinDelta { get; set; } = 500;

    /// <summary>Gets or sets how many days daily snapshots remain active before archival.</summary>
    [Range(7, 3650)]
    public int RetentionDays { get; set; } = 180;

    /// <summary>Gets or sets the shared HNMO state key used for row storage.</summary>
    [StringLength(260)]
    public string FileName { get; set; } = HnmoStateStore.StateFileName;
}
