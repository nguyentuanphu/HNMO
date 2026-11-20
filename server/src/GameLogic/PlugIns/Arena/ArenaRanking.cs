// <copyright file="ArenaRanking.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Arena;

using System.Globalization;

/// <summary>
/// The HNMO Arena's ranked 1v1 (pure, no game): Elo ratings, the Sydney week, the results and the leaderboards.
/// </summary>
public static class ArenaRanking
{
    /// <summary>The rating every account starts with.</summary>
    public const int StartRating = 1000;

    /// <summary>How much one duel can move a rating (Elo's K).</summary>
    public const int K = 32;

    private static readonly TimeZoneInfo Sydney = ResolveSydney();

    /// <summary>The new ratings after a duel (Elo).</summary>
    /// <param name="winner">The winner's rating.</param>
    /// <param name="loser">The loser's rating.</param>
    /// <returns>Both new ratings.</returns>
    public static (int Winner, int Loser) Rate(int winner, int loser)
    {
        var expected = 1.0 / (1.0 + Math.Pow(10, (loser - winner) / 400.0));
        var change = Math.Max(1, (int)Math.Round(K * (1 - expected), MidpointRounding.AwayFromZero));
        return (winner + change, Math.Max(0, loser - change));
    }

    /// <summary>The Sydney week of a time, e.g. "2026-W47" (weeks start on Monday, Sydney time).</summary>
    /// <param name="utc">The time (UTC).</param>
    /// <returns>The week key.</returns>
    public static string WeekKey(DateTime utc)
    {
        var sydney = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Sydney);
        return $"{ISOWeek.GetYear(sydney)}-W{ISOWeek.GetWeekOfYear(sydney):00}";
    }

    /// <summary>The Sydney day of a time, e.g. "2026-11-20" (the daily points cap counts these).</summary>
    /// <param name="utc">The time (UTC).</param>
    /// <returns>The day.</returns>
    public static string SydneyDay(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Sydney).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>The widest rating gap two queued players are matched over: 100, and 50 more for every 10 s waited.</summary>
    /// <param name="waited">How long the one waiting longer has waited.</param>
    /// <returns>The gap.</returns>
    public static int MatchGap(TimeSpan waited) => (int)Math.Min(1000, 100 + (50 * Math.Floor(Math.Max(0, waited.TotalSeconds) / 10)));

    /// <summary>Records a duel's result for both accounts.</summary>
    /// <param name="data">The stored data (changed).</param>
    /// <param name="winner">The winner (login, character, class).</param>
    /// <param name="loser">The loser (login, character, class).</param>
    /// <param name="utcNow">The time.</param>
    /// <returns>Both records after the duel.</returns>
    public static (ArenaPlayerRecord Winner, ArenaPlayerRecord Loser) Record(
        ArenaStoreData data,
        (string Login, string Character, string ClassName) winner,
        (string Login, string Character, string ClassName) loser,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(data);
        var week = WeekKey(utcNow);
        var w = Get(data, winner.Login, week);
        var l = Get(data, loser.Login, week);
        (w.Rating, l.Rating) = Rate(w.Rating, l.Rating);
        w.Wins++;
        w.WeekWins++;
        l.Losses++;
        l.WeekLosses++;
        (w.CharacterName, w.ClassName, w.LastMatchUtc) = (winner.Character, winner.ClassName, utcNow);
        (l.CharacterName, l.ClassName, l.LastMatchUtc) = (loser.Character, loser.ClassName, utcNow);
        return (w, l);
    }

    /// <summary>The record of an account, as it stands (a new one isn't stored).</summary>
    /// <param name="data">The stored data.</param>
    /// <param name="login">The login name.</param>
    /// <param name="utcNow">The time (the week's counts of an older week read as 0).</param>
    /// <returns>The record.</returns>
    public static ArenaPlayerRecord Of(ArenaStoreData data, string login, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (!data.Players.TryGetValue(login.ToLowerInvariant(), out var record))
        {
            return new ArenaPlayerRecord { Login = login, WeekKey = WeekKey(utcNow) };
        }

        return record.WeekKey == WeekKey(utcNow) ? record : new ArenaPlayerRecord
        {
            Login = record.Login, CharacterName = record.CharacterName, ClassName = record.ClassName, Rating = record.Rating,
            Wins = record.Wins, Losses = record.Losses, WeekKey = WeekKey(utcNow), LastMatchUtc = record.LastMatchUtc,
        };
    }

    /// <summary>The leaderboards: all time by rating, this week by wins (then rating).</summary>
    /// <param name="data">The stored data.</param>
    /// <param name="utcNow">The time.</param>
    /// <param name="count">How many of each.</param>
    /// <returns>Both lists.</returns>
    public static (IReadOnlyList<ArenaPlayerRecord> AllTime, IReadOnlyList<ArenaPlayerRecord> Week) Leaderboards(ArenaStoreData data, DateTime utcNow, int count = 20)
    {
        ArgumentNullException.ThrowIfNull(data);
        var played = data.Players.Values.Where(p => p.Wins + p.Losses > 0).ToList();
        var allTime = played.OrderByDescending(p => p.Rating).ThenByDescending(p => p.Wins).ThenBy(p => p.CharacterName, StringComparer.Ordinal).Take(count).ToList();
        var week = WeekKey(utcNow);
        var thisWeek = played.Where(p => p.WeekKey == week && p.WeekWins + p.WeekLosses > 0)
            .OrderByDescending(p => p.WeekWins).ThenByDescending(p => p.Rating).ThenBy(p => p.CharacterName, StringComparer.Ordinal).Take(count).ToList();
        return (allTime, thisWeek);
    }

    private static ArenaPlayerRecord Get(ArenaStoreData data, string login, string week)
    {
        var key = login.ToLowerInvariant();
        if (!data.Players.TryGetValue(key, out var record))
        {
            record = new ArenaPlayerRecord { Login = login, WeekKey = week };
            data.Players[key] = record;
        }

        if (record.WeekKey != week)
        {
            (record.WeekKey, record.WeekWins, record.WeekLosses) = (week, 0, 0);
        }

        return record;
    }

    private static TimeZoneInfo ResolveSydney()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time");
        }
    }
}
