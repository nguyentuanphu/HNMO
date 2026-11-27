// <copyright file="ActivityLogConfiguration.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ActivityLog;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Configuration for <see cref="ActivityLogPlugIn"/> (ROADMAP 14.9 player activity log).
/// </summary>
public sealed class ActivityLogConfiguration
{
    /// <summary>Gets or sets a value indicating whether activity logging is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether only valuable item drops and pickups are logged.</summary>
    public bool OnlyValuableDropsAndPickups { get; set; } = true;

    /// <summary>Gets or sets minimum item level considered valuable for non-excellent/non-jewel drops and pickups.</summary>
    [Range(1, 15)]
    public int MinValuableDropLevel { get; set; } = 7;

    /// <summary>Gets or sets how many days activity log rows remain active before automated archival.</summary>
    [Range(1, 3650)]
    public int RetentionDays { get; set; } = 30;

    /// <summary>Gets or sets the shared HNMO state key used to locate the database row backend.</summary>
    [StringLength(260)]
    public string FileName { get; set; } = HnmoStateStore.StateFileName;
}
