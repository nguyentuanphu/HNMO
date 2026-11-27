// <copyright file="TimetableStateWriter.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Timetable;

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

/// <summary>
/// Serializes <see cref="TimetableSnapshot"/> into plain-ASCII JSON for packet <c>F5 33</c> and <c>/api/website/timetable</c>.
/// </summary>
public static class TimetableStateWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.Default,
        WriteIndented = false,
    };

    /// <summary>Serializes a snapshot into plain ASCII JSON.</summary>
    /// <param name="snapshot">The timetable snapshot.</param>
    /// <returns>Plain-ASCII JSON string.</returns>
    public static string Write(TimetableSnapshot snapshot)
    {
        var payload = new
        {
            v = 1,
            off = !snapshot.Enabled,
            now = new DateTimeOffset(DateTime.SpecifyKind(snapshot.NowUtc, DateTimeKind.Utc)).ToUnixTimeMilliseconds(),
            timeZone = snapshot.TimeZoneId,
            entries = snapshot.Entries.Select(e => new
            {
                id = e.Id,
                category = e.Category,
                nameEn = e.NameEn,
                nameVi = e.NameVi,
                mapsEn = e.MapsEn,
                mapsVi = e.MapsVi,
                dropsEn = e.DropsEn,
                dropsVi = e.DropsVi,
                schedule = e.ScheduleText,
                durationMin = e.DurationMinutes,
                active = e.IsActive,
                nextStart = e.NextStartUtc is { } next
                    ? new DateTimeOffset(DateTime.SpecifyKind(next, DateTimeKind.Utc)).ToUnixTimeMilliseconds()
                    : (long?)null,
                endsAt = e.EndsAtUtc is { } end
                    ? new DateTimeOffset(DateTime.SpecifyKind(end, DateTimeKind.Utc)).ToUnixTimeMilliseconds()
                    : (long?)null,
            }).ToList(),
            findGmWinners = snapshot.FindGmWinners.Select(w => new
            {
                name = w.CharacterName,
                map = w.MapName,
                coins = w.Coins,
                coinType = w.CoinType,
                zen = w.Zen,
                at = new DateTimeOffset(DateTime.SpecifyKind(w.WonAtUtc, DateTimeKind.Utc)).ToUnixTimeMilliseconds(),
            }).ToList(),
        };

        return EscapeNonAscii(JsonSerializer.Serialize(payload, JsonOptions));
    }

    private static string EscapeNonAscii(string json)
    {
        var sb = new StringBuilder(json.Length);
        foreach (var c in json)
        {
            if (c <= 0x7F)
            {
                sb.Append(c);
            }
            else
            {
                sb.Append("\\u");
                sb.Append(((int)c).ToString("x4"));
            }
        }

        return sb.ToString();
    }
}
