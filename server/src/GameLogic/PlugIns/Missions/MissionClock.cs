// <copyright file="MissionClock.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Missions;

using System.Globalization;

/// <summary>
/// The game days of the missions: a day starts at the reset hour in the configured time zone
/// (00:00 Sydney time by default), a week on Monday at that hour, and a month on its first day at that hour.
/// </summary>
public sealed class MissionClock
{
    private static readonly TimeZoneInfo Fallback = TimeZoneInfo.CreateCustomTimeZone("HNMO+10", TimeSpan.FromHours(10), "UTC+10", "UTC+10");

    /// <summary>
    /// Initializes a new instance of the <see cref="MissionClock"/> class.
    /// </summary>
    /// <param name="timeZone">The IANA name of the time zone.</param>
    /// <param name="resetHour">The reset hour, 0 to 23.</param>
    public MissionClock(string? timeZone, int resetHour)
    {
        this.TimeZone = Find(timeZone);
        this.ResetHour = Math.Clamp(resetHour, 0, 23);
    }

    /// <summary>Gets the time zone.</summary>
    public TimeZoneInfo TimeZone { get; }

    /// <summary>Gets the reset hour.</summary>
    public int ResetHour { get; }

    /// <summary>Formats a game day as its key.</summary>
    /// <param name="day">The day.</param>
    /// <returns>The key.</returns>
    public static string Key(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Parses a key of <see cref="Key"/>.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The day, or <c>null</c>.</returns>
    public static DateOnly? Parse(string? key) =>
        DateOnly.TryParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day : null;

    /// <summary>
    /// Gets the game day of a moment.
    /// </summary>
    /// <param name="utc">The moment (UTC).</param>
    /// <returns>The game day.</returns>
    public DateOnly Day(DateTime utc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), this.TimeZone);
        return DateOnly.FromDateTime(local.AddHours(-this.ResetHour));
    }

    /// <summary>
    /// Gets the Monday of the game week of a moment.
    /// </summary>
    /// <param name="utc">The moment (UTC).</param>
    /// <returns>The Monday.</returns>
    public DateOnly WeekStart(DateTime utc)
    {
        var day = this.Day(utc);
        return day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
    }

    /// <summary>Gets the key of the game day of a moment, e.g. "2026-11-15".</summary>
    /// <param name="utc">The moment (UTC).</param>
    /// <returns>The key.</returns>
    public string DayKey(DateTime utc) => Key(this.Day(utc));

    /// <summary>Gets the key of the game week of a moment: its Monday, e.g. "2026-11-09".</summary>
    /// <param name="utc">The moment (UTC).</param>
    /// <returns>The key.</returns>
    public string WeekKey(DateTime utc) => Key(this.WeekStart(utc));

    /// <summary>Gets the key of the game month of a moment, e.g. "2026-11".</summary>
    /// <param name="utc">The moment (UTC).</param>
    /// <returns>The key.</returns>
    public string MonthKey(DateTime utc) => this.Day(utc).ToString("yyyy-MM", CultureInfo.InvariantCulture);

    /// <summary>Gets the number of days of the game month of a moment.</summary>
    /// <param name="utc">The moment (UTC).</param>
    /// <returns>The days.</returns>
    public int DaysInMonth(DateTime utc)
    {
        var day = this.Day(utc);
        return DateTime.DaysInMonth(day.Year, day.Month);
    }

    /// <summary>Gets the moment (UTC) when the next game day starts.</summary>
    /// <param name="utc">The moment (UTC).</param>
    /// <returns>The next reset.</returns>
    public DateTime NextDayStart(DateTime utc) => this.StartOf(this.Day(utc).AddDays(1));

    /// <summary>Gets the moment (UTC) when the next game week starts.</summary>
    /// <param name="utc">The moment (UTC).</param>
    /// <returns>The next reset.</returns>
    public DateTime NextWeekStart(DateTime utc) => this.StartOf(this.WeekStart(utc).AddDays(7));

    /// <summary>
    /// Gets the moment (UTC) when a game day starts.
    /// </summary>
    /// <param name="day">The game day.</param>
    /// <returns>The moment.</returns>
    public DateTime StartOf(DateOnly day)
    {
        var local = day.ToDateTime(new TimeOnly(this.ResetHour, 0), DateTimeKind.Unspecified);

        // A reset hour which doesn't exist on a daylight saving day starts an hour later.
        while (this.TimeZone.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, this.TimeZone);
    }

    private static TimeZoneInfo Find(string? id)
    {
        foreach (var candidate in new[] { id, "Australia/Sydney", "AUS Eastern Standard Time" })
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(candidate.Trim());
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                // try the next one
            }
        }

        return Fallback;
    }
}
