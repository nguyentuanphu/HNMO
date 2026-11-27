// <copyright file="CatchupCapStateWriter.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.CatchupCap;

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

/// <summary>
/// Serializes Catch-up Level &amp; Reset Cap state into plain-ASCII JSON for packet <c>F5 3D</c> and <c>/api/website/catchup</c>.
/// </summary>
public static class CatchupCapStateWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.Default,
        WriteIndented = false,
    };

    /// <summary>Serializes today's cap snapshot.</summary>
    /// <param name="engine">Catch-up engine.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>Plain-ASCII JSON.</returns>
    public static string WritePublic(CatchupCapEngine engine, DateTime utcNow)
    {
        var snap = engine.GetSnapshot(utcNow);
        var cfg = engine.Configuration;
        var payload = new
        {
            v = 1,
            enabled = engine.Enabled,
            now = ToMs(utcNow),
            nextDayAt = ToMs(snap.NextDayStartsAtUtc),
            levelCap = new
            {
                enabled = snap.LevelCapEnabled,
                todayCap = snap.TodayLevelCap,
                initialCap = snap.InitialLevelCap,
                hardMax = snap.HardMaxLevelCap,
                perDay = cfg.LevelsPerDay,
                triggered = snap.LevelTriggered,
                firstPlayer = snap.LevelFirstPlayer,
            },
            resetCap = new
            {
                enabled = snap.ResetCapEnabled,
                todayDailyLimit = snap.TodayDailyResetCap,
                initialDailyLimit = cfg.InitialDailyResets,
                perDay = cfg.DailyResetsPerDay,
                hardMax = cfg.HardMaxDailyResets,
                triggerResets = cfg.TriggerResets,
                triggered = snap.ResetTriggered,
                firstPlayer = snap.ResetFirstPlayer,
            },
        };

        return EscapeNonAscii(JsonSerializer.Serialize(payload, JsonOptions));
    }

    private static long ToMs(DateTime utc) =>
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

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
