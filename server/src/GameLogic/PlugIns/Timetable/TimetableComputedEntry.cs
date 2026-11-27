// <copyright file="TimetableComputedEntry.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Timetable;

using System.Globalization;
using HNMO.GameLogic.PlugIns.InvasionEvents;
using HNMO.GameLogic.PlugIns.PeriodicTasks;
using HNMO.GameLogic.PlugIns.WorldBosses;

/// <summary>
/// Computed row on the event and boss timetable.
/// </summary>
public sealed class TimetableComputedEntry
{
    /// <summary>Gets the unique identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the category.</summary>
    public required string Category { get; init; }

    /// <summary>Gets the English name.</summary>
    public required string NameEn { get; init; }

    /// <summary>Gets the Vietnamese name.</summary>
    public required string NameVi { get; init; }

    /// <summary>Gets the English maps summary.</summary>
    public required string MapsEn { get; init; }

    /// <summary>Gets the Vietnamese maps summary.</summary>
    public required string MapsVi { get; init; }

    /// <summary>Gets the English drops summary.</summary>
    public required string DropsEn { get; init; }

    /// <summary>Gets the Vietnamese drops summary.</summary>
    public required string DropsVi { get; init; }

    /// <summary>Gets the formatted daily schedule times (wall-clock in Sydney / configured time zone).</summary>
    public required string ScheduleText { get; init; }

    /// <summary>Gets the duration in minutes.</summary>
    public int DurationMinutes { get; init; }

    /// <summary>Gets a value indicating whether this entry is active right now.</summary>
    public bool IsActive { get; init; }

    /// <summary>Gets the next start time in UTC (or current start when active if unknown).</summary>
    public DateTime? NextStartUtc { get; init; }

    /// <summary>Gets the current run's end time in UTC when active.</summary>
    public DateTime? EndsAtUtc { get; init; }
}
