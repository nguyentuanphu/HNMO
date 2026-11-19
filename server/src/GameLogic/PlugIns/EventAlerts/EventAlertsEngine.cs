// <copyright file="EventAlertsEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.EventAlerts;

using System.Globalization;
using HNMO.GameLogic.PlugIns.Timetable;

/// <summary>
/// Pure rule evaluation engine for <see cref="EventAlertsPlugIn"/>: determines upcoming/start alerts,
/// matches subscriber preferences, and formats localized EN/VI Sydney-time notifications.
/// </summary>
public static class EventAlertsEngine
{
    private static readonly TimeSpan ToleranceWindow = TimeSpan.FromSeconds(45);

    /// <summary>Normalizes a timetable entry into a standardized alert filter category key.</summary>
    /// <param name="entry">The computed timetable entry.</param>
    /// <returns>Normalized category key (<c>blood_castle</c>, <c>devil_square</c>, <c>chaos_castle</c>, <c>world_boss</c>, or <c>invasion</c>).</returns>
    public static string ClassifyCategoryKey(TimetableComputedEntry entry)
    {
        var id = (entry.Id ?? string.Empty).ToLowerInvariant();
        if (id.Contains("blood", StringComparison.Ordinal))
        {
            return "blood_castle";
        }

        if (id.Contains("devil", StringComparison.Ordinal))
        {
            return "devil_square";
        }

        if (id.Contains("chaos", StringComparison.Ordinal))
        {
            return "chaos_castle";
        }

        if (string.Equals(entry.Category, "boss", StringComparison.OrdinalIgnoreCase)
            || id.Contains("boss", StringComparison.Ordinal))
        {
            return "world_boss";
        }

        if (string.Equals(entry.Category, "invasion", StringComparison.OrdinalIgnoreCase))
        {
            return "invasion";
        }

        return id.Length > 0 ? id : "event";
    }

    /// <summary>Checks whether <paramref name="subscription"/> matches <paramref name="categoryKey"/>.</summary>
    /// <param name="subscription">Subscriber configuration.</param>
    /// <param name="categoryKey">Event category key.</param>
    /// <returns><c>true</c> when subscribed (empty filter list subscribes to all events).</returns>
    public static bool MatchesSubscription(EventAlertSubscription subscription, string categoryKey)
    {
        if (subscription.Events is null || subscription.Events.Count == 0)
        {
            return true;
        }

        return subscription.Events.Any(e =>
            string.Equals(e, "all", StringComparison.OrdinalIgnoreCase)
            || string.Equals(e, categoryKey, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Evaluates upcoming and opening event alert candidates from a <see cref="TimetableSnapshot"/>.</summary>
    /// <param name="config">Alert configuration.</param>
    /// <param name="store">Store holding already-dispatched deduplication keys.</param>
    /// <param name="snapshot">Current timetable snapshot.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>List of new unsent alert candidates.</returns>
    public static IReadOnlyList<EventAlertCandidate> CollectDueAlerts(
        EventAlertsConfiguration config,
        EventAlertStore store,
        TimetableSnapshot snapshot,
        DateTime utcNow)
    {
        if (!config.Enabled)
        {
            return [];
        }

        var results = new List<EventAlertCandidate>();
        var leadMinutes = Math.Clamp(config.LeadTimeMinutes, 0, 30);
        var tz = ResolveSydneyZone();

        foreach (var entry in snapshot.Entries)
        {
            var catKey = ClassifyCategoryKey(entry);

            if (leadMinutes > 0 && !entry.IsActive && entry.NextStartUtc is { } nextUtc)
            {
                var remaining = nextUtc - utcNow;
                var leadSpan = TimeSpan.FromMinutes(leadMinutes);
                if (remaining > TimeSpan.Zero && remaining <= leadSpan + ToleranceWindow && remaining >= leadSpan - ToleranceWindow)
                {
                    var key = $"{entry.Id}:{ToEpochMinute(nextUtc)}:soon:{leadMinutes}";
                    if (!store.HasSent(key))
                    {
                        var mins = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
                        var sydneyClock = FormatSydneyTime(nextUtc, tz);
                        results.Add(new EventAlertCandidate(
                            key,
                            catKey,
                            $"Starting Soon: {entry.NameEn}",
                            $"Sắp diễn ra: {entry.NameVi}",
                            $"Starts in ~{mins} min ({sydneyClock} Sydney) — {entry.MapsEn}.",
                            $"Bắt đầu sau ~{mins} phút ({sydneyClock} giờ Sydney) — {entry.MapsVi}."));
                    }
                }
            }

            if (config.RemindAtStart && entry.IsActive)
            {
                var anchorUtc = entry.NextStartUtc ?? utcNow;
                var key = $"{entry.Id}:{ToEpochMinute(anchorUtc)}:start";
                if (!store.HasSent(key))
                {
                    var sydneyClock = FormatSydneyTime(utcNow, tz);
                    results.Add(new EventAlertCandidate(
                        key,
                        catKey,
                        $"Active Now: {entry.NameEn}",
                        $"Đang mở: {entry.NameVi}",
                        $"Now open ({sydneyClock} Sydney) — {entry.MapsEn}!",
                        $"Đã mở ({sydneyClock} giờ Sydney) — {entry.MapsVi}!"));
                }
            }
        }

        return results;
    }

    private static long ToEpochMinute(DateTime utc) =>
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds() / 60;

    private static TimeZoneInfo ResolveSydneyZone()
    {
        foreach (var id in new[] { "Australia/Sydney", "AUS Eastern Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }

    private static string FormatSydneyTime(DateTime utc, TimeZoneInfo tz)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);
        return local.ToString("HH:mm", CultureInfo.InvariantCulture);
    }
}
