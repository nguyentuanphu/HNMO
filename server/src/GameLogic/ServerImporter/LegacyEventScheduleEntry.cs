// <copyright file="LegacyEventScheduleEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ServerImporter;

using System;
using System.Collections.Generic;

/// <summary>Parsed event schedule window.</summary>
public sealed class LegacyEventScheduleEntry
{
    /// <summary>Gets or sets event key identifier.</summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>Gets or sets event name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether this event exists only in Season 9 and not in HNMO's Season 6 data.</summary>
    public bool IsSeason9Only { get; set; }

    /// <summary>Gets or sets optional map number.</summary>
    public short? MapNumber { get; set; }

    /// <summary>Gets or sets comma-separated HH:mm schedule times.</summary>
    public string ScheduleTimes { get; set; } = string.Empty;

    /// <summary>Gets or sets optional EXP bonus percent.</summary>
    public int ExpBonusPercent { get; set; }

    /// <summary>
    /// Gets or sets the drop bonus. For a bonus window (<see cref="IsBonusWindow"/>) it is the servers' ItemDropBonus:
    /// percentage points added to the base item drop chance (ItemDropPer), not a percentage of it.
    /// </summary>
    public int DropBonusPercent { get; set; }

    /// <summary>Gets or sets a value indicating whether this is a bonus window ("happy hour", IGC_BonusEvent.xml) rather than an event start.</summary>
    public bool IsBonusWindow { get; set; }

    /// <summary>Gets or sets how long a bonus window lasts, in minutes (from its StartHour to its EndHour).</summary>
    public int DurationMinutes { get; set; } = 60;

    /// <summary>Gets or sets a value indicating whether the times were read from the server's files (not fallback times).</summary>
    public bool HasRealTimes { get; set; }

    /// <summary>Gets or sets the day of the week a bonus window runs on (0 = Sunday), or -1 for every day.</summary>
    public int DayOfWeek { get; set; } = -1;

    /// <summary>Gets or sets optional reward description.</summary>
    public string RewardSummary { get; set; } = string.Empty;
}
