// <copyright file="SeasonPlanStateWriter.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.SeasonPlan;

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

/// <summary>
/// Serializes Season Plan timeline, active gates, and unlock conditions into plain-ASCII JSON for <c>F5 3F</c> and <c>/api/website/season-plan</c>.
/// </summary>
public static class SeasonPlanStateWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.Default,
        WriteIndented = false,
    };

    /// <summary>Serializes the public Season Plan timeline and live gate states.</summary>
    /// <param name="engine">Season Plan engine.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>Plain-ASCII JSON.</returns>
    public static string WritePublic(SeasonPlanEngine engine, DateTime utcNow)
    {
        if (engine.Enabled)
        {
            engine.EvaluateConditions(utcNow);
        }

        var snap = engine.Store.Read(d => (
            Unlocked: new Dictionary<string, DateTime>(d.UnlockedStages, StringComparer.OrdinalIgnoreCase),
            Reasons: new Dictionary<string, string>(d.UnlockReasons, StringComparer.OrdinalIgnoreCase),
            BossKills: new Dictionary<string, int>(d.BossKills, StringComparer.OrdinalIgnoreCase),
            Levels: d.CharacterLevels.Values.ToList()));

        var gates = Enum.GetValues<SeasonFeatureGate>()
            .ToDictionary(g => g.ToString(), g => engine.IsFeatureUnlocked(g, utcNow));

        var stages = engine.Configuration.Stages.Select(s =>
        {
            var isUnlocked = snap.Unlocked.TryGetValue(s.Id, out var at);
            snap.Reasons.TryGetValue(s.Id, out var reason);
            var currentBossKills = !string.IsNullOrWhiteSpace(s.RequiredBossId) && snap.BossKills.TryGetValue(s.RequiredBossId.Trim(), out var bk) ? bk : 0;
            var currentPlayers = s.RequiredPlayerLevel > 0 ? snap.Levels.Count(l => l >= s.RequiredPlayerLevel) : 0;

            return new
            {
                id = s.Id,
                nameEn = s.NameEn,
                nameVi = s.NameVi,
                descriptionEn = s.DescriptionEn,
                descriptionVi = s.DescriptionVi,
                unlocks = s.Unlocks.Select(u => u.ToString()).ToList(),
                unlocked = isUnlocked,
                unlockedAt = isUnlocked ? ToMs(at) : (long?)null,
                reason = reason ?? string.Empty,
                unlockAt = s.UnlockAtUtc is { } u ? ToMs(u) : (long?)null,
                requiredBossId = s.RequiredBossId,
                requiredBossKills = s.RequiredBossKills,
                currentBossKills,
                requiredPlayerLevel = s.RequiredPlayerLevel,
                requiredPlayersCount = s.RequiredPlayersCount,
                currentPlayers,
                maxBoxLevel = s.MaxBoxLevel,
            };
        }).ToList();

        var payload = new
        {
            v = 1,
            enabled = engine.Enabled,
            now = ToMs(utcNow),
            maxBoxLevel = engine.GetCurrentMaxBoxLevel(utcNow),
            gates,
            stages,
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
