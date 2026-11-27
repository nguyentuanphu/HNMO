// <copyright file="ArenaGuildWarEngine.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Arena;

using System.Globalization;
using HNMO.GameLogic.PlugIns.WorldBosses;

/// <summary>
/// The rules of the Arena's guild war, without the game (owner's choices, 2026-11-20): on set Sydney days and times two
/// guilds fight on the Valley of Loren; each kill is a point, and capture points (flags) add a point every few seconds to
/// the guild holding them; the guild with more points at the end wins. Guilds are paired by their guild war record.
/// </summary>
public static class ArenaGuildWarEngine
{
    private static readonly string[] DayNames = ["sun", "mon", "tue", "wed", "thu", "fri", "sat"];

    /// <summary>Reads days like "Sat, Sun" (English names or 0-6, Sunday = 0).</summary>
    /// <param name="text">The text.</param>
    /// <param name="days">The days.</param>
    /// <returns><c>true</c>, if every part is a day.</returns>
    public static bool TryParseDays(string? text, out List<DayOfWeek> days)
    {
        days = [];
        foreach (var part in (text ?? string.Empty).Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var key = part.ToLowerInvariant();
            var index = Array.FindIndex(DayNames, d => key.StartsWith(d, StringComparison.Ordinal));
            if (index < 0 && int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number is >= 0 and <= 6)
            {
                index = number;
            }

            if (index < 0)
            {
                return false;
            }

            if (!days.Contains((DayOfWeek)index))
            {
                days.Add((DayOfWeek)index);
            }
        }

        return true;
    }

    /// <summary>Reads flags like "79,27; 89,27; 100,33".</summary>
    /// <param name="text">The text.</param>
    /// <param name="flags">The flags' tiles.</param>
    /// <returns><c>true</c>, if every part is a tile.</returns>
    public static bool TryParseFlags(string? text, out List<(byte X, byte Y)> flags)
    {
        flags = [];
        foreach (var part in (text ?? string.Empty).Split([';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var xy = part.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);
            if (xy.Length != 2 || !byte.TryParse(xy[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) || !byte.TryParse(xy[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
            {
                return false;
            }

            flags.Add((x, y));
        }

        return flags.Count > 0;
    }

    /// <summary>The next start at or after a moment: the given Sydney times on the given days.</summary>
    /// <param name="days">The days.</param>
    /// <param name="times">The times.</param>
    /// <param name="timeZone">Sydney's time zone.</param>
    /// <param name="afterUtc">The moment (UTC).</param>
    /// <returns>The next start (UTC), or <c>null</c> without days or times.</returns>
    public static DateTime? NextStart(IReadOnlyList<DayOfWeek> days, IReadOnlyList<TimeOnly> times, TimeZoneInfo timeZone, DateTime afterUtc)
    {
        ArgumentNullException.ThrowIfNull(days);
        ArgumentNullException.ThrowIfNull(times);
        if (days.Count == 0 || times.Count == 0)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(afterUtc, DateTimeKind.Utc), timeZone));
        for (var day = today.AddDays(-1); day <= today.AddDays(8); day = day.AddDays(1))
        {
            if (!days.Contains(day.DayOfWeek))
            {
                continue;
            }

            var next = times.Select(t => WorldBossSchedule.ToUtc(day, t, timeZone)).Where(t => t >= afterUtc).OrderBy(t => t).FirstOrDefault();
            if (next != default)
            {
                return next;
            }
        }

        return null;
    }

    /// <summary>Pairs the guilds by their record: the best two fight, then the next two; an odd one out has no opponent.</summary>
    /// <param name="guilds">The guilds with their record (wins, then fewer losses, rank higher).</param>
    /// <returns>The pairs and the guild left over, if any.</returns>
    public static (IReadOnlyList<(string A, string B)> Pairs, string? Left) Pair(IEnumerable<(string Guild, int Wins, int Losses)> guilds)
    {
        ArgumentNullException.ThrowIfNull(guilds);
        var ordered = guilds.OrderByDescending(g => g.Wins).ThenBy(g => g.Losses).ThenBy(g => g.Guild, StringComparer.Ordinal).Select(g => g.Guild).ToList();
        var pairs = new List<(string, string)>();
        for (var i = 0; i + 1 < ordered.Count; i += 2)
        {
            pairs.Add((ordered[i], ordered[i + 1]));
        }

        return (pairs, ordered.Count % 2 == 1 ? ordered[^1] : null);
    }

    /// <summary>
    /// One second of a flag: who stands within reach decides. A side alone at the flag builds up its hold; after the
    /// capture time the flag is theirs. Both sides there (or none) stop the build-up.
    /// </summary>
    /// <param name="flag">The flag's state (owner 0/1/2, the side building up, the seconds built).</param>
    /// <param name="sideA">Fighters of side 1 standing at the flag.</param>
    /// <param name="sideB">Fighters of side 2 standing at the flag.</param>
    /// <param name="captureSeconds">The seconds alone it takes.</param>
    /// <returns>The flag after the second.</returns>
    public static Flag Step(Flag flag, int sideA, int sideB, int captureSeconds)
    {
        ArgumentNullException.ThrowIfNull(flag);
        var alone = sideA > 0 && sideB == 0 ? 1 : sideB > 0 && sideA == 0 ? 2 : 0;
        if (alone == 0 || alone == flag.Owner)
        {
            return flag with { Taking = 0, Seconds = 0 };
        }

        var seconds = flag.Taking == alone ? flag.Seconds + 1 : 1;
        return seconds >= Math.Max(1, captureSeconds) ? flag with { Owner = alone, Taking = 0, Seconds = 0 } : flag with { Taking = alone, Seconds = seconds };
    }

    /// <summary>The winner by points: 1, -1 for side 2, 0 for a draw.</summary>
    /// <param name="scoreA">Side 1's points.</param>
    /// <param name="scoreB">Side 2's points.</param>
    /// <returns>The winner.</returns>
    public static int Winner(int scoreA, int scoreB) => Math.Sign(scoreA - scoreB);

    /// <summary>A capture point.</summary>
    /// <param name="X">The tile's x.</param>
    /// <param name="Y">The tile's y.</param>
    /// <param name="Owner">The side holding it (0: nobody).</param>
    /// <param name="Taking">The side building up its hold (0: nobody).</param>
    /// <param name="Seconds">The seconds built up.</param>
    public sealed record Flag(byte X, byte Y, int Owner = 0, int Taking = 0, int Seconds = 0);
}
