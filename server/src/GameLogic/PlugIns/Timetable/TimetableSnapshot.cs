// <copyright file="TimetableSnapshot.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Timetable;

using System.Globalization;
using HNMO.GameLogic.PlugIns.InvasionEvents;
using HNMO.GameLogic.PlugIns.PeriodicTasks;
using HNMO.GameLogic.PlugIns.WorldBosses;

/// <summary>
/// Full computed timetable snapshot.
/// </summary>
public sealed class TimetableSnapshot
{
    /// <summary>Gets a value indicating whether the timetable plug-in is enabled.</summary>
    public bool Enabled { get; init; }

    /// <summary>Gets the snapshot timestamp in UTC.</summary>
    public DateTime NowUtc { get; init; }

    /// <summary>Gets the resolved schedule time zone ID (e.g. <c>Australia/Sydney</c>).</summary>
    public required string TimeZoneId { get; init; }

    /// <summary>Gets the computed entries sorted by active state, then earliest next start.</summary>
    public required IReadOnlyList<TimetableComputedEntry> Entries { get; init; }

    /// <summary>Gets recent Find the GM winners.</summary>
    public required IReadOnlyList<FindGmWinnerEntry> FindGmWinners { get; init; }
}
