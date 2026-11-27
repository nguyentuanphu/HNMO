// <copyright file="ArenaRoyaleEngine.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Arena;

using HNMO.GameLogic.PlugIns.WorldBosses;

/// <summary>
/// The rules of the Arena's battle royale, without the game (owner's choices, 2026-11-20): matches at set Sydney times on
/// Lorencia, sign-up in the minutes before, one life, a safe zone that shrinks towards a point chosen at the start
/// (outside it, and on the town's safe tiles, players lose health every second), the last one standing wins.
/// </summary>
public static class ArenaRoyaleEngine
{
    /// <summary>The next start at or after a moment, from Sydney wall-clock times (a time missing on the daylight saving day moves on to the first valid minute).</summary>
    /// <param name="times">The start times of a day (Sydney).</param>
    /// <param name="timeZone">Sydney's time zone.</param>
    /// <param name="afterUtc">The moment (UTC).</param>
    /// <returns>The next start (UTC), or <c>null</c> without times.</returns>
    public static DateTime? NextStart(IReadOnlyList<TimeOnly> times, TimeZoneInfo timeZone, DateTime afterUtc)
    {
        ArgumentNullException.ThrowIfNull(times);
        ArgumentNullException.ThrowIfNull(timeZone);
        if (times.Count == 0)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(afterUtc, DateTimeKind.Utc), timeZone));
        for (var day = today.AddDays(-1); day <= today.AddDays(2); day = day.AddDays(1))
        {
            var next = times.Select(t => WorldBossSchedule.ToUtc(day, t, timeZone)).Where(t => t >= afterUtc).OrderBy(t => t).FirstOrDefault();
            if (next != default)
            {
                return next;
            }
        }

        return null;
    }

    /// <summary>
    /// The safe zone at a moment of the match: the whole field at first, then (after the grace time) a circle that
    /// shrinks steadily and moves from the field's centre to the final centre, reaching the final radius at the shrink's end.
    /// </summary>
    /// <param name="elapsed">The time since the start.</param>
    /// <param name="settings">The match settings.</param>
    /// <param name="startCentre">The field's centre.</param>
    /// <param name="finalCentre">Where the zone closes in (chosen at the start).</param>
    /// <returns>The zone's centre and radius (tiles).</returns>
    public static Zone ZoneAt(TimeSpan elapsed, Settings settings, (double X, double Y) startCentre, (double X, double Y) finalCentre)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var shrink = Math.Max(1, settings.ShrinkMinutes * 60.0);
        var t = Math.Clamp((elapsed.TotalSeconds - (settings.GraceMinutes * 60.0)) / shrink, 0, 1);
        var radius = settings.StartRadius + ((settings.FinalRadius - settings.StartRadius) * t);
        return new Zone(startCentre.X + ((finalCentre.X - startCentre.X) * t), startCentre.Y + ((finalCentre.Y - startCentre.Y) * t), radius);
    }

    /// <summary>The share of the maximum health lost each second outside the zone: grows from the first to the last value over the match.</summary>
    /// <param name="elapsed">The time since the start.</param>
    /// <param name="settings">The match settings.</param>
    /// <returns>The share (0..1).</returns>
    public static double DamageShare(TimeSpan elapsed, Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var t = Math.Clamp(elapsed.TotalMinutes / Math.Max(1, settings.Minutes), 0, 1);
        return (settings.FirstDamagePercent + ((settings.LastDamagePercent - settings.FirstDamagePercent) * t)) / 100.0;
    }

    /// <summary>Whether a tile is inside a zone.</summary>
    /// <param name="zone">The zone.</param>
    /// <param name="x">The tile's x.</param>
    /// <param name="y">The tile's y.</param>
    /// <returns><c>true</c>, if inside (or on the edge).</returns>
    public static bool Inside(Zone zone, int x, int y) => Math.Pow(x - zone.X, 2) + Math.Pow(y - zone.Y, 2) <= zone.Radius * zone.Radius;

    /// <summary>
    /// The places of the players at the end: the survivors first (by kills, then by health share), then the fallen, the last
    /// to fall first. Place 1 wins.
    /// </summary>
    /// <param name="survivors">The players still standing, with their kills and health share.</param>
    /// <param name="fallen">The fallen, in the order they fell (first fallen first).</param>
    /// <returns>The logins from place 1 down.</returns>
    public static IReadOnlyList<string> Places(IEnumerable<(string Login, int Kills, double Health)> survivors, IEnumerable<string> fallen)
    {
        ArgumentNullException.ThrowIfNull(survivors);
        ArgumentNullException.ThrowIfNull(fallen);
        return survivors.OrderByDescending(s => s.Kills).ThenByDescending(s => s.Health).Select(s => s.Login)
            .Concat(fallen.Reverse())
            .ToList();
    }

    /// <summary>The arena points of a place: the winner's, everyone else's (owner's choice: only the winner gets many).</summary>
    /// <param name="place">The place (1 wins).</param>
    /// <param name="settings">The match settings.</param>
    /// <returns>The points.</returns>
    public static int PointsOf(int place, Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return place == 1 ? settings.PointsWinner : settings.PointsOthers;
    }

    /// <summary>A safe zone: centre and radius in tiles.</summary>
    /// <param name="X">The centre's x.</param>
    /// <param name="Y">The centre's y.</param>
    /// <param name="Radius">The radius.</param>
    public sealed record Zone(double X, double Y, double Radius);

    /// <summary>The settings of a match (from <see cref="ArenaConfiguration"/>).</summary>
    /// <param name="Minutes">The longest a match lasts.</param>
    /// <param name="GraceMinutes">The minutes before the zone starts to shrink.</param>
    /// <param name="ShrinkMinutes">The minutes the zone takes to shrink to the final radius.</param>
    /// <param name="StartRadius">The radius at first.</param>
    /// <param name="FinalRadius">The radius at the end.</param>
    /// <param name="FirstDamagePercent">The share of health lost each second outside the zone at the start.</param>
    /// <param name="LastDamagePercent">The share at the end.</param>
    /// <param name="PointsWinner">The winner's arena points.</param>
    /// <param name="PointsOthers">Everyone else's arena points.</param>
    public sealed record Settings(int Minutes, int GraceMinutes, int ShrinkMinutes, int StartRadius, int FinalRadius, double FirstDamagePercent, double LastDamagePercent, int PointsWinner, int PointsOthers);
}
