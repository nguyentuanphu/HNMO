// <copyright file="MarriageStateWriter.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Marriage;

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

/// <summary>
/// Serializes Marriage state into plain-ASCII JSON for packet <c>F5 37</c> and <c>/api/website/marriage</c>.
/// </summary>
public static class MarriageStateWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.Default,
        WriteIndented = false,
    };

    /// <summary>Writes the public couples list and rules for the website.</summary>
    /// <param name="engine">The marriage engine.</param>
    /// <param name="utcNow">Current UTC time.</param>
    /// <returns>Plain-ASCII JSON.</returns>
    public static string WritePublic(MarriageEngine engine, DateTime utcNow) =>
        WriteInternal(engine, characterName: null, bonusActiveNow: false, utcNow, null, null, null);

    /// <summary>Writes player-specific marital state for packet <c>F5 37</c>.</summary>
    /// <param name="engine">The marriage engine.</param>
    /// <param name="characterName">The player's character name.</param>
    /// <param name="bonusActiveNow">Whether couple party bonus is active right now.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <param name="actionResult">Optional action result code.</param>
    /// <param name="messageEn">Optional English notice.</param>
    /// <param name="messageVi">Optional Vietnamese notice.</param>
    /// <returns>Plain-ASCII JSON.</returns>
    public static string WritePlayerState(
        MarriageEngine engine,
        string characterName,
        bool bonusActiveNow,
        DateTime utcNow,
        string? actionResult = null,
        string? messageEn = null,
        string? messageVi = null) =>
        WriteInternal(engine, characterName, bonusActiveNow, utcNow, actionResult, messageEn, messageVi);

    private static string WriteInternal(
        MarriageEngine engine,
        string? characterName,
        bool bonusActiveNow,
        DateTime utcNow,
        string? actionResult,
        string? messageEn,
        string? messageVi)
    {
        var cfg = engine.Configuration;
        var couples = engine.AllCouples();
        MarriageCoupleEntry? myCouple = null;
        string? spouseName = null;
        PendingMarriageProposal? incoming = null;
        PendingMarriageProposal? outgoing = null;

        if (!string.IsNullOrWhiteSpace(characterName))
        {
            myCouple = engine.GetCoupleOf(characterName);
            spouseName = myCouple is null
                ? null
                : string.Equals(myCouple.PartnerAName, characterName, StringComparison.OrdinalIgnoreCase)
                    ? myCouple.PartnerBName
                    : myCouple.PartnerAName;
            incoming = engine.GetIncomingProposal(characterName, utcNow);
            outgoing = engine.GetOutgoingProposal(characterName, utcNow);
        }

        var defaultEn = spouseName is null ? string.Empty : cfg.DefaultTitleEn.Replace("{spouse}", spouseName, StringComparison.Ordinal);
        var defaultVi = spouseName is null ? string.Empty : cfg.DefaultTitleVi.Replace("{spouse}", spouseName, StringComparison.Ordinal);
        var titleEn = string.IsNullOrWhiteSpace(myCouple?.CustomTitle) ? defaultEn : myCouple.CustomTitle;
        var titleVi = string.IsNullOrWhiteSpace(myCouple?.CustomTitle) ? defaultVi : myCouple.CustomTitle;

        var payload = new
        {
            v = 1,
            enabled = engine.Enabled,
            now = ToMs(utcNow),
            rules = new
            {
                minLevel = cfg.MinimumLevel,
                requireCeremonyLocation = cfg.RequireCeremonyLocation,
                ceremonyMapNumber = cfg.CeremonyMapNumber,
                ceremonyX = cfg.CeremonyX,
                ceremonyY = cfg.CeremonyY,
                ceremonyRadius = cfg.CeremonyRadius,
                ceremonyZenCost = cfg.CeremonyZenCost,
                allowCustomTitle = cfg.AllowCustomTitle,
                partyExpBonusPercent = cfg.CoupleExpBonusPercent,
                partyDefenseBonus = cfg.CoupleDefenseBonus,
                divorceZenFee = cfg.DivorceZenFee,
                remarryCooldownHours = cfg.RemarryCooldownHours,
                allowSpouseWarp = cfg.AllowSpouseWarp,
            },
            me = characterName is null
                ? null
                : new
                {
                    character = characterName,
                    married = myCouple is not null,
                    spouse = spouseName,
                    titleEn,
                    titleVi,
                    customTitle = myCouple?.CustomTitle ?? string.Empty,
                    marriedAt = myCouple is not null ? ToMs(myCouple.MarriedAtUtc) : 0L,
                    bonusActiveNow,
                    incomingProposalFrom = incoming?.ProposerName,
                    outgoingProposalTo = outgoing?.TargetName,
                },
            couples = couples.Take(100).Select(c => new
            {
                id = c.Id,
                partnerA = c.PartnerAName,
                partnerB = c.PartnerBName,
                title = c.CustomTitle,
                marriedAt = ToMs(c.MarriedAtUtc),
                days = Math.Max(0, (int)Math.Floor((utcNow - c.MarriedAtUtc).TotalDays)),
            }).ToList(),
            @event = actionResult is null ? null : new { result = actionResult, textEn = messageEn, textVi = messageVi },
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
