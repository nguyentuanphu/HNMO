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

    /// <summary>Gets or sets whether this event is Season 9-only and unavailable on HNMO's S6 dataset.</summary>
    public bool IsSeason9Only { get; set; }

    /// <summary>Gets or sets optional map number.</summary>
    public short? MapNumber { get; set; }

    /// <summary>Gets or sets comma-separated HH:mm schedule times.</summary>
    public string ScheduleTimes { get; set; } = string.Empty;

    /// <summary>Gets or sets optional EXP bonus percent.</summary>
    public int ExpBonusPercent { get; set; }

    /// <summary>Gets or sets optional drop bonus percent.</summary>
    public int DropBonusPercent { get; set; }

    /// <summary>Gets or sets optional reward description.</summary>
    public string RewardSummary { get; set; } = string.Empty;
}
