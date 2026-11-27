// <copyright file="WorldBossSchedule.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.WorldBosses;

using System.Globalization;

/// <summary>
/// The spawn times of a world boss, in the wall-clock time of the configured time zone (Sydney by default), so a boss
/// at 20:00 stays at 20:00 when daylight saving time starts or ends. A time which doesn't exist on the day the clocks
/// go forward moves to the first valid minute after it; a time which happens twice when they go back counts once.
/// </summary>
public static class WorldBossSchedule
{
    /// <summary>
    /// Parses "HH:mm" times separated by "," or ";".
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="times">The times, ordered and without duplicates.</param>
    /// <returns><c>true</c>, if the text is valid.</returns>
    public static bool TryParseTimes(string? text, out List<TimeOnly> times)
    {
        var result = new SortedSet<TimeOnly>();
        times = new List<TimeOnly>();
        foreach (var part in (text ?? string.Empty).Split([',', ';', ' ', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TimeOnly.TryParseExact(part, ["H:mm", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            {
                return false;
            }

            result.Add(time);
        }

        times = result.ToList();
        return true;
    }

    /// <summary>
    /// Formats times as "HH:mm, HH:mm".
    /// </summary>
    /// <param name="times">The times.</param>
    /// <returns>The text.</returns>
    public static string FormatTimes(IEnumerable<TimeOnly> times) => string.Join(", ", times.Select(t => t.ToString("HH:mm", CultureInfo.InvariantCulture)));

    /// <summary>
    /// Gets the times of a day at which the boss spawns (wall-clock time).
    /// </summary>
    /// <param name="boss">The boss.</param>
    /// <returns>The times.</returns>
    public static List<TimeOnly> TimesOfDay(WorldBossDefinition boss)
    {
        if (boss.ScheduleKind == WorldBossScheduleKind.FixedTimes)
        {
            return TryParseTimes(boss.FixedTimes, out var fixedTimes) ? fixedTimes : new List<TimeOnly>();
        }

        if (!TryParseTimes(boss.StartTime, out var start) || start.Count == 0)
        {
            start = [new TimeOnly(0, 0)];
        }

        var every = Math.Clamp(boss.EveryHours, 1, 24);
        var times = new List<TimeOnly>();
        for (var minute = (start[0].Hour * 60) + start[0].Minute; minute < 24 * 60; minute += every * 60)
        {
            times.Add(new TimeOnly(minute / 60, minute % 60));
        }

        return times;
    }

    /// <summary>
    /// Gets the first spawn time after a moment.
    /// </summary>
    /// <param name="boss">The boss.</param>
    /// <param name="timeZone">The time zone of the schedule.</param>
    /// <param name="afterUtc">The moment (UTC); the result is later than it.</param>
    /// <returns>The spawn time (UTC), or <c>null</c> when the schedule has no time.</returns>
    public static DateTime? Next(WorldBossDefinition boss, TimeZoneInfo timeZone, DateTime afterUtc)
    {
        var times = TimesOfDay(boss);
        if (times.Count == 0)
        {
            return null;
        }

        afterUtc = DateTime.SpecifyKind(afterUtc, DateTimeKind.Utc);
        var days = boss.Days?.Count > 0 ? boss.Days.ToHashSet() : null;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(afterUtc, timeZone));
        for (var offset = -1; offset <= 8; offset++)
        {
            var day = today.AddDays(offset);
            if (days is not null && !days.Contains(day.DayOfWeek))
            {
                continue;
            }

            foreach (var time in times)
            {
                var utc = ToUtc(day, time, timeZone);
                if (utc > afterUtc)
                {
                    return utc;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Converts a wall-clock time of a day to UTC.
    /// </summary>
    /// <param name="day">The day.</param>
    /// <param name="time">The time.</param>
    /// <param name="timeZone">The time zone.</param>
    /// <returns>The moment (UTC).</returns>
    public static DateTime ToUtc(DateOnly day, TimeOnly time, TimeZoneInfo timeZone)
    {
        var local = day.ToDateTime(time, DateTimeKind.Unspecified);
        while (timeZone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }

        if (timeZone.IsAmbiguousTime(local))
        {
            // The first of the two (the daylight saving one, the larger offset).
            var offset = timeZone.GetAmbiguousTimeOffsets(local).Max();
            return DateTime.SpecifyKind(local - offset, DateTimeKind.Utc);
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
    }
}
