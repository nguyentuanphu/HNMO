// <copyright file="PartyOptionsStateWriter.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.PartyOptions;

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

/// <summary>
/// Serializes Party Password &amp; Auto-Party hunting spot listings into plain-ASCII JSON for <c>F5 45</c> and <c>/api/website/party-spots</c>.
/// </summary>
public static class PartyOptionsStateWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.Default,
        WriteIndented = false,
    };

    /// <summary>Serializes public spot advertisements from persisted snapshot (passwords never exposed).</summary>
    /// <param name="configuration">Configuration.</param>
    /// <param name="store">Persistent store.</param>
    /// <returns>Plain-ASCII JSON.</returns>
    public static string WritePublic(PartyOptionsConfiguration configuration, PartyOptionsStore store)
    {
        var ads = store.ReadAds().Select(a => new
        {
            leaderName = a.LeaderName,
            mapName = a.MapName,
            spotName = a.SpotName,
            minLevel = a.MinLevel,
            maxLevel = a.MaxLevel,
            hasPassword = a.HasPassword,
            memberCount = a.MemberCount,
            maxSize = a.MaxSize,
        }).ToList();

        var payload = new
        {
            v = 1,
            enabled = true,
            allowPasswordJoin = configuration.AllowPasswordJoin,
            allowAutoSpotParty = configuration.AllowAutoSpotParty,
            huntingSpots = configuration.HuntingSpots,
            ads,
        };

        return EscapeNonAscii(JsonSerializer.Serialize(payload, JsonOptions));
    }

    /// <summary>Serializes per-player party options state with live advertisements and eligibility.</summary>
    /// <param name="engine">Engine.</param>
    /// <param name="player">Player.</param>
    /// <param name="notice">Optional action notice.</param>
    /// <returns>Plain-ASCII JSON.</returns>
    public static string WriteForPlayer(
        PartyOptionsEngine engine,
        Player player,
        (string Status, string MessageEn, string MessageVi)? notice = null)
    {
        var heroName = player.SelectedCharacter?.Name ?? string.Empty;
        var level = (int)(player.Attributes?[HNMO.GameLogic.Attributes.Stats.Level] ?? 1);
        var hasParty = player.Party is not null;
        var isLeader = player.Party?.PartyMaster?.Name == heroName || !hasParty;
        var leaderKey = player.Party?.PartyMaster?.Name ?? heroName;
        var ownAd = engine.GetAdvertisement(leaderKey);

        var ads = engine.ListAdvertisements().Select(a => new
        {
            leaderName = a.LeaderName,
            mapName = a.MapName,
            spotName = a.SpotName,
            minLevel = a.MinLevel,
            maxLevel = a.MaxLevel,
            hasPassword = !string.IsNullOrEmpty(a.Password),
            memberCount = a.MemberCount,
            maxSize = a.MaxSize,
            canJoin = !hasParty
                && engine.PlanSpotJoin(a.LeaderName, password: string.Empty, level, hasParty, out _) == PartyJoinCheckResult.Ok,
        }).ToList();

        var payload = new
        {
            v = 1,
            enabled = engine.Enabled,
            allowPasswordJoin = engine.Configuration.AllowPasswordJoin,
            allowAutoSpotParty = engine.Configuration.AllowAutoSpotParty,
            huntingSpots = engine.Configuration.HuntingSpots,
            hasParty,
            isLeader,
            ownAd = ownAd is null ? null : new
            {
                spotName = ownAd.SpotName,
                hasPassword = !string.IsNullOrEmpty(ownAd.Password),
                minLevel = ownAd.MinLevel,
                maxLevel = ownAd.MaxLevel,
            },
            notice = notice is { } n ? new { status = n.Status, messageEn = n.MessageEn, messageVi = n.MessageVi } : null,
            ads,
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
