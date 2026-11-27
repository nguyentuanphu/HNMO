// <copyright file="CatchupCapConfiguration.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.CatchupCap;

using System.ComponentModel.DataAnnotations;
using HNMO.GameLogic.PlugIns;

/// <summary>
/// Configuration for <see cref="CatchupCapPlugIn"/> (OFF by default).
/// </summary>
public sealed class CatchupCapConfiguration
{
    /// <summary>Gets or sets the shared HNMO state file name.</summary>
    [StringLength(260)]
    public string FileName { get; set; } = HnmoStateStore.StateFileName;

    /// <summary>Gets or sets the Sydney-based time zone ID used for daily cap ticks.</summary>
    [StringLength(64)]
    public string TimeZone { get; set; } = "Australia/Sydney";

    /// <summary>Gets or sets the local hour (0..23) when each day's cap rises.</summary>
    [Range(0, 23)]
    public int DayStartHour { get; set; }

    /// <summary>Gets or sets a value indicating whether the level catch-up cap is enforced.</summary>
    public bool LevelCapEnabled { get; set; } = true;

    /// <summary>Gets or sets the starting level cap before the first player hits <see cref="InitialLevelCap"/>.</summary>
    [Range(1, 1000)]
    public int InitialLevelCap { get; set; } = 300;

    /// <summary>Gets or sets how many levels today's cap increases each Sydney day once triggered.</summary>
    [Range(0, 400)]
    public int LevelsPerDay { get; set; } = 10;

    /// <summary>Gets or sets the ceiling level cap (e.g. server max level 400).</summary>
    [Range(1, 1000)]
    public int HardMaxLevelCap { get; set; } = 400;

    /// <summary>Gets or sets a value indicating whether the daily reset cap mode is enforced.</summary>
    public bool ResetCapEnabled { get; set; }

    /// <summary>Gets or sets the starting daily reset limit before anyone triggers the rise (or Day 0 daily limit).</summary>
    [Range(0, 500)]
    public int InitialDailyResets { get; set; } = 3;

    /// <summary>Gets or sets how many resets/day the daily reset cap increases per day once triggered.</summary>
    [Range(0, 100)]
    public int DailyResetsPerDay { get; set; } = 1;

    /// <summary>Gets or sets the hard ceiling on daily resets (0 = no upper cap).</summary>
    [Range(0, 1000)]
    public int HardMaxDailyResets { get; set; } = 20;

    /// <summary>Gets or sets how many resets the first leader must reach to trigger daily reset cap growth (0 = active from day 0).</summary>
    [Range(0, 10000)]
    public int TriggerResets { get; set; } = 5;

    /// <summary>Gets or sets a value indicating whether global in-game chat notices announce when first player triggers growth.</summary>
    public bool AnnounceInGame { get; set; } = true;
}
