// <copyright file="TimetableEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Timetable;

using System.Globalization;
using HNMO.GameLogic.PlugIns.InvasionEvents;
using HNMO.GameLogic.PlugIns.PeriodicTasks;
using HNMO.GameLogic.PlugIns.WorldBosses;

/// <summary>
/// Describes a scheduled source item (mini-game, invasion, world boss, or custom event) for <see cref="TimetableEngine"/>.
/// </summary>
public sealed class TimetableSourceItem
{
    /// <summary>Gets or sets the unique key.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the category (<c>event</c>, <c>invasion</c>, <c>boss</c>, or <c>custom</c>).</summary>
    public string Category { get; set; } = "event";

    /// <summary>Gets or sets the English name.</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese name.</summary>
    public string NameVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the English map names.</summary>
    public string MapsEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese map names.</summary>
    public string MapsVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the English drop/reward summary.</summary>
    public string DropsEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese drop/reward summary.</summary>
    public string DropsVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the daily schedule times (wall-clock in server time zone).</summary>
    public IReadOnlyList<TimeOnly> DailyTimes { get; set; } = [];

    /// <summary>Gets or sets optional allowed days of the week (empty means every day).</summary>
    public IReadOnlyList<DayOfWeek> AllowedDays { get; set; } = [];

    /// <summary>Gets or sets the duration of each occurrence.</summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(20);

    /// <summary>Gets or sets an optional precomputed next start time in UTC.</summary>
    public DateTime? ExplicitNextStartUtc { get; set; }

    /// <summary>Gets or sets an optional active end time in UTC if the event is currently running.</summary>
    public DateTime? ExplicitEndsAtUtc { get; set; }

    /// <summary>Gets or sets a value indicating whether the source is currently live on a server.</summary>
    public bool IsLiveNow { get; set; }
}

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

/// <summary>
/// Pure rule engine that computes next occurrences, active windows, and readable schedules in Sydney/wall-clock time.
/// </summary>
public static class TimetableEngine
{
    /// <summary>Resolves a time zone identifier across Linux/macOS IANA and Windows IDs, falling back to Sydney then UTC.</summary>
    /// <param name="timeZoneId">The requested time zone ID.</param>
    /// <returns>The resolved <see cref="TimeZoneInfo"/>.</returns>
    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            candidates.Add(timeZoneId.Trim());
        }

        candidates.Add("Australia/Sydney");
        candidates.Add("AUS Eastern Standard Time");

        foreach (var candidate in candidates)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(candidate);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }

    /// <summary>Parses a comma/semicolon-separated list of HH:mm wall-clock times.</summary>
    /// <param name="text">The raw times text.</param>
    /// <returns>Sorted distinct times of day.</returns>
    public static List<TimeOnly> ParseTimes(string? text)
    {
        return WorldBossSchedule.TryParseTimes(text, out var parsed)
            ? parsed
            : [];
    }

    /// <summary>Builds a sorted timetable snapshot from configured sources.</summary>
    /// <param name="configuration">The timetable configuration.</param>
    /// <param name="enabled">Whether the timetable feature is enabled.</param>
    /// <param name="utcNow">The current UTC time.</param>
    /// <param name="sources">The enabled event/invasion/boss sources.</param>
    /// <param name="findGmWinners">Recent Find the GM winners.</param>
    /// <returns>The computed snapshot.</returns>
    public static TimetableSnapshot BuildSnapshot(
        TimetableConfiguration configuration,
        bool enabled,
        DateTime utcNow,
        IEnumerable<TimetableSourceItem> sources,
        IReadOnlyList<FindGmWinnerEntry> findGmWinners)
    {
        utcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        var timeZone = ResolveTimeZone(configuration.TimeZoneId);
        var merged = new Dictionary<string, TimetableSourceItem>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources)
        {
            merged[source.Id] = source;
        }

        foreach (var custom in configuration.CustomEntries)
        {
            if (!custom.Enabled || string.IsNullOrWhiteSpace(custom.NameEn))
            {
                continue;
            }

            var id = string.IsNullOrWhiteSpace(custom.Id)
                ? $"custom-{custom.NameEn.Trim().ToLowerInvariant().Replace(' ', '-')}"
                : custom.Id.Trim();
            var times = ParseTimes(custom.ScheduleTimes);
            if (merged.TryGetValue(id, out var existing))
            {
                merged[id] = new TimetableSourceItem
                {
                    Id = id,
                    Category = string.IsNullOrWhiteSpace(custom.Category) ? existing.Category : custom.Category,
                    NameEn = string.IsNullOrWhiteSpace(custom.NameEn) ? existing.NameEn : custom.NameEn,
                    NameVi = string.IsNullOrWhiteSpace(custom.NameVi) ? existing.NameVi : custom.NameVi,
                    MapsEn = string.IsNullOrWhiteSpace(custom.MapsEn) ? existing.MapsEn : custom.MapsEn,
                    MapsVi = string.IsNullOrWhiteSpace(custom.MapsVi) ? existing.MapsVi : custom.MapsVi,
                    DropsEn = string.IsNullOrWhiteSpace(custom.DropsEn) ? existing.DropsEn : custom.DropsEn,
                    DropsVi = string.IsNullOrWhiteSpace(custom.DropsVi) ? existing.DropsVi : custom.DropsVi,
                    DailyTimes = times.Count > 0 ? times : existing.DailyTimes,
                    AllowedDays = existing.AllowedDays,
                    Duration = custom.DurationMinutes > 0 ? TimeSpan.FromMinutes(custom.DurationMinutes) : existing.Duration,
                    ExplicitNextStartUtc = existing.ExplicitNextStartUtc,
                    ExplicitEndsAtUtc = existing.ExplicitEndsAtUtc,
                    IsLiveNow = existing.IsLiveNow,
                };
            }
            else
            {
                merged[id] = new TimetableSourceItem
                {
                    Id = id,
                    Category = string.IsNullOrWhiteSpace(custom.Category) ? "custom" : custom.Category,
                    NameEn = custom.NameEn.Trim(),
                    NameVi = string.IsNullOrWhiteSpace(custom.NameVi) ? custom.NameEn.Trim() : custom.NameVi.Trim(),
                    MapsEn = custom.MapsEn.Trim(),
                    MapsVi = string.IsNullOrWhiteSpace(custom.MapsVi) ? custom.MapsEn.Trim() : custom.MapsVi.Trim(),
                    DropsEn = custom.DropsEn.Trim(),
                    DropsVi = string.IsNullOrWhiteSpace(custom.DropsVi) ? custom.DropsEn.Trim() : custom.DropsVi.Trim(),
                    DailyTimes = times,
                    Duration = TimeSpan.FromMinutes(Math.Clamp(custom.DurationMinutes, 1, 720)),
                };
            }
        }

        var computed = new List<TimetableComputedEntry>();
        foreach (var item in merged.Values)
        {
            var (isActive, endsAtUtc, nextStartUtc) = ResolveTiming(item, timeZone, utcNow);
            computed.Add(new TimetableComputedEntry
            {
                Id = item.Id,
                Category = item.Category,
                NameEn = item.NameEn,
                NameVi = string.IsNullOrWhiteSpace(item.NameVi) ? item.NameEn : item.NameVi,
                MapsEn = string.IsNullOrWhiteSpace(item.MapsEn) ? "Multiple maps" : item.MapsEn,
                MapsVi = string.IsNullOrWhiteSpace(item.MapsVi) ? item.MapsEn : item.MapsVi,
                DropsEn = item.DropsEn,
                DropsVi = string.IsNullOrWhiteSpace(item.DropsVi) ? item.DropsEn : item.DropsVi,
                ScheduleText = item.DailyTimes.Count > 0
                    ? string.Join(", ", item.DailyTimes.Order().Distinct().Select(t => t.ToString("HH:mm", CultureInfo.InvariantCulture)))
                    : string.Empty,
                DurationMinutes = Math.Max(0, (int)Math.Round(item.Duration.TotalMinutes)),
                IsActive = isActive,
                NextStartUtc = nextStartUtc,
                EndsAtUtc = endsAtUtc,
            });
        }

        computed.Sort((a, b) =>
        {
            var activeCmp = b.IsActive.CompareTo(a.IsActive);
            if (activeCmp != 0)
            {
                return activeCmp;
            }

            if (a.NextStartUtc.HasValue && b.NextStartUtc.HasValue)
            {
                var timeCmp = a.NextStartUtc.Value.CompareTo(b.NextStartUtc.Value);
                if (timeCmp != 0)
                {
                    return timeCmp;
                }
            }
            else if (a.NextStartUtc.HasValue != b.NextStartUtc.HasValue)
            {
                return a.NextStartUtc.HasValue ? -1 : 1;
            }

            return string.Compare(a.NameEn, b.NameEn, StringComparison.OrdinalIgnoreCase);
        });

        return new TimetableSnapshot
        {
            Enabled = enabled,
            NowUtc = utcNow,
            TimeZoneId = string.IsNullOrWhiteSpace(configuration.TimeZoneId) ? "Australia/Sydney" : configuration.TimeZoneId,
            Entries = computed,
            FindGmWinners = findGmWinners,
        };
    }

    /// <summary>Computes whether an item is currently active and its next UTC start time.</summary>
    /// <param name="item">The timetable item.</param>
    /// <param name="timeZone">The schedule wall-clock time zone.</param>
    /// <param name="utcNow">The current UTC time.</param>
    /// <returns>Active flag, end timestamp if active, and next start timestamp.</returns>
    public static (bool IsActive, DateTime? EndsAtUtc, DateTime? NextStartUtc) ResolveTiming(
        TimetableSourceItem item,
        TimeZoneInfo timeZone,
        DateTime utcNow)
    {
        if (item.IsLiveNow)
        {
            return (true, item.ExplicitEndsAtUtc, item.ExplicitNextStartUtc);
        }

        if (item.DailyTimes.Count == 0)
        {
            return (false, item.ExplicitEndsAtUtc, item.ExplicitNextStartUtc);
        }

        var duration = item.Duration > TimeSpan.Zero ? item.Duration : TimeSpan.FromMinutes(15);
        var allowedDays = item.AllowedDays.Count > 0 ? item.AllowedDays.ToHashSet() : null;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone));
        var isActive = false;
        DateTime? activeEndsAt = null;
        var nextStart = item.ExplicitNextStartUtc;

        for (var offset = -1; offset <= 8; offset++)
        {
            var day = today.AddDays(offset);
            if (allowedDays is not null && !allowedDays.Contains(day.DayOfWeek))
            {
                continue;
            }

            foreach (var time in item.DailyTimes)
            {
                var startUtc = ToUtcWallClock(day, time, timeZone);
                var endUtc = startUtc + duration;
                if (utcNow >= startUtc && utcNow < endUtc)
                {
                    isActive = true;
                    activeEndsAt = endUtc;
                }

                if (startUtc > utcNow && (nextStart is null || startUtc < nextStart.Value))
                {
                    nextStart = startUtc;
                }
            }
        }

        return (isActive, activeEndsAt ?? item.ExplicitEndsAtUtc, nextStart);
    }

    private static DateTime ToUtcWallClock(DateOnly day, TimeOnly time, TimeZoneInfo timeZone)
    {
        var local = day.ToDateTime(time, DateTimeKind.Unspecified);
        while (timeZone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }

        if (timeZone.IsAmbiguousTime(local))
        {
            var offset = timeZone.GetAmbiguousTimeOffsets(local).Max();
            return DateTime.SpecifyKind(local - offset, DateTimeKind.Utc);
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
    }
}
