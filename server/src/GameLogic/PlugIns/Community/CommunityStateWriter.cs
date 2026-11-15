// <copyright file="CommunityStateWriter.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Community;

using System.Text.Json;
using System.Text.Json.Serialization;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// Something that happened, which the web game tells in the chat.
/// </summary>
/// <param name="Type">"discord", "zalo", "code", "goods" or "claim".</param>
/// <param name="Result">The result, e.g. "Ok", "AlreadyPaid", "NotLinked", "Unknown", "BagFull".</param>
/// <param name="Coins">The coins given.</param>
/// <param name="EventId">The community event.</param>
public sealed record CommunityStateEvent(string Type, string? Result = null, int Coins = 0, string? EventId = null);

/// <summary>
/// What the state of a player is written from (besides the engine).
/// </summary>
/// <param name="Login">The login name.</param>
/// <param name="Character">The character name.</param>
/// <param name="DiscordLinkAvailable">Whether the game server hands out Discord link codes (the Discord integration is on).</param>
/// <param name="DiscordLinked">Whether the account is linked to a Discord user.</param>
public sealed record CommunityPlayerFacts(string Login, string Character, bool DiscordLinkAvailable, bool DiscordLinked);

/// <summary>
/// Writes the community state as JSON: a player's for the game client (HnmoCommunityState), and the public one for the
/// website (no account data). Non-ASCII text is escaped, so the JSON is plain ASCII.
/// </summary>
public static class CommunityStateWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Writes the state of a player.
    /// </summary>
    /// <param name="engine">The engine.</param>
    /// <param name="facts">The player's facts.</param>
    /// <param name="nowUtc">The time.</param>
    /// <param name="stateEvent">What happened, if anything.</param>
    /// <param name="itemName">The name of an item, if known.</param>
    /// <returns>The JSON.</returns>
    public static string WriteState(CommunityEngine engine, CommunityPlayerFacts facts, DateTime nowUtc, CommunityStateEvent? stateEvent, Func<short, short, string?>? itemName = null)
    {
        var configuration = engine.Configuration;
        var account = engine.Account(facts.Login);
        var state = new
        {
            V = 1,
            Now = Ms(nowUtc),
            Discord = new
            {
                On = configuration.Discord.Enabled && configuration.Discord.Coins > 0,
                configuration.Discord.Coins,
                configuration.Discord.CoinType,
                LinkAvailable = facts.DiscordLinkAvailable,
                Linked = facts.DiscordLinked,
                Paid = account?.DiscordPaidAtUtc is not null,
            },
            Zalo = new
            {
                On = configuration.Zalo.Enabled && configuration.Zalo.Coins > 0,
                configuration.Zalo.Coins,
                configuration.Zalo.CoinType,
                Status = account?.ZaloStatus ?? ZaloClaimStatus.None,
                Code = account?.ZaloStatus is ZaloClaimStatus.Pending or ZaloClaimStatus.Approved ? account.ZaloCode : null,
                Name = account?.ZaloName,
            },
            Events = engine.Events(nowUtc).Where(e => e.Phase != CommunityEventPhase.Ended || engine.Took(e.Definition.Id, facts.Login, facts.Character) is (true, _) or (_, true))
                .Select(e =>
                {
                    var (entered, winner) = engine.Took(e.Definition.Id, facts.Login, facts.Character);
                    return Event(engine, e.Definition, e.Phase, itemName, entered, winner);
                }).ToList(),
            Owed = engine.Owed(facts.Character).Select(o => new { o.EventId, o.TitleEn, o.TitleVi, Reward = Reward(o.Reward, itemName) }).ToList(),
            Event = stateEvent,
        };
        return JsonSerializer.Serialize(state, Options);
    }

    /// <summary>
    /// Writes the public state for the website: the rewards, the events which are shown there (not their codes or
    /// winners' accounts), and the newest highlights.
    /// </summary>
    /// <param name="engine">The engine.</param>
    /// <param name="nowUtc">The time.</param>
    /// <param name="itemName">The name of an item, if known.</param>
    /// <returns>The JSON.</returns>
    public static string WritePublic(CommunityEngine engine, DateTime nowUtc, Func<short, short, string?>? itemName = null)
    {
        var configuration = engine.Configuration;
        var state = new
        {
            V = 1,
            Enabled = true,
            Now = Ms(nowUtc),
            TimeZone = configuration.TimeZone,
            Discord = new { On = configuration.Discord.Enabled && configuration.Discord.Coins > 0, configuration.Discord.Coins, configuration.Discord.CoinType },
            Zalo = new { On = configuration.Zalo.Enabled && configuration.Zalo.Coins > 0, configuration.Zalo.Coins, configuration.Zalo.CoinType },
            Events = engine.Events(nowUtc).Where(e => e.Definition.ShowOnWebsite && (e.Phase != CommunityEventPhase.Ended || engine.ToUtc(e.Definition.End) is { } end && nowUtc - end < TimeSpan.FromDays(7)))
                .Select(e => new
                {
                    e.Definition.Id,
                    e.Definition.TitleEn,
                    e.Definition.TitleVi,
                    DescriptionEn = e.Definition.DescriptionEn,
                    DescriptionVi = e.Definition.DescriptionVi,
                    Start = engine.ToUtc(e.Definition.Start) is { } s ? Ms(s) : (long?)null,
                    End = engine.ToUtc(e.Definition.End) is { } x ? Ms(x) : (long?)null,
                    e.Phase,
                    HasCode = !string.IsNullOrWhiteSpace(e.Definition.Code),
                    Winners = e.Phase != CommunityEventPhase.Upcoming ? e.Definition.Winners.Where(w => !string.IsNullOrWhiteSpace(w)).Select(w => w.Trim()).ToList() : [],
                    Reward = Reward(e.Definition.Reward, itemName),
                }).ToList(),
            Highlights = engine.Highlights(configuration.HighlightsShown).Select(h => new { At = Ms(h.AtUtc), h.Kind, h.TextEn, h.TextVi }).ToList(),
        };
        return JsonSerializer.Serialize(state, Options);
    }

    private static object Event(CommunityEngine engine, CommunityEventDefinition d, CommunityEventPhase phase, Func<short, short, string?>? itemName, bool entered, bool winner) => new
    {
        d.Id,
        d.TitleEn,
        d.TitleVi,
        DescriptionEn = d.DescriptionEn,
        DescriptionVi = d.DescriptionVi,
        Start = engine.ToUtc(d.Start) is { } s ? Ms(s) : (long?)null,
        End = engine.ToUtc(d.End) is { } e ? Ms(e) : (long?)null,
        Phase = phase,
        HasCode = !string.IsNullOrWhiteSpace(d.Code),
        Entered = entered,
        Winner = winner,
        Reward = Reward(d.Reward, itemName),
    };

    private static object Reward(MissionReward reward, Func<short, short, string?>? itemName) => new
    {
        reward.Coins,
        reward.CoinType,
        reward.Zen,
        Items = reward.Items.Where(i => i.Count > 0).Select(i => new { i.Group, i.Number, i.Level, i.Count, Name = itemName?.Invoke(i.Group, i.Number) }).ToList(),
    };

    private static long Ms(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
}
