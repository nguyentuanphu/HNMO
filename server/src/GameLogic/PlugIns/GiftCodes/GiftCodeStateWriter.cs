// <copyright file="GiftCodeStateWriter.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GiftCodes;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Writes the gift codes as JSON: the state of a player for the game client (HnmoGiftCodesState), the answer of a
/// redemption on the website, and the public codes for the website (no account data). Non-ASCII text is escaped, so
/// the JSON is plain ASCII.
/// </summary>
public static class GiftCodeStateWriter
{
    /// <summary>The account's last redemptions sent to the game client.</summary>
    public const int ClientRecent = 5;

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
    /// <param name="login">The account's login name.</param>
    /// <param name="characterId">The character's id.</param>
    /// <param name="outcome">The outcome of a code the player entered, if any.</param>
    /// <param name="eventType">What happened: "redeem" (a code was entered), "goods" (waiting rewards given), "claim" (the window's button).</param>
    /// <param name="eventResult">The result of a "goods" or "claim" event: "Ok", "BagFull" or "Nothing".</param>
    /// <param name="itemName">Gives the name of an item.</param>
    /// <returns>The JSON.</returns>
    public static string WriteState(GiftCodeEngine engine, string login, string characterId, GiftCodeOutcome? outcome, string? eventType, string? eventResult, Func<short, short, string?>? itemName = null)
    {
        var state = new
        {
            v = 1,
            on = true,
            website = engine.Configuration.AllowWebsite,
            owed = engine.Owed(characterId).Select(o => new { code = o.Code, zen = o.Zen, items = o.Items.Select(i => Item(i, itemName)) }),
            recent = engine.LogOf(login, ClientRecent).Select(e => new { code = e.Code, at = ToMs(e.AtUtc), character = e.CharacterName, source = e.Source }),
            @event = eventType is null ? null : new
            {
                type = eventType,
                result = outcome is not null ? outcome.Result.ToString() : eventResult,
                code = outcome?.Entry?.Code,
                reward = outcome is { Result: GiftCodeResult.Ok, Code: { } code } ? Reward(code.Reward, itemName) : null,
                waiting = outcome?.Owed is { } owed && engine.Owed(characterId).Any(o => o.Reference == owed.Reference),
                triesLeft = outcome?.TriesLeft,
                retryAt = outcome?.RetryAtUtc is { } retry ? ToMs(retry) : (long?)null,
                details = outcome?.Code is { } c && outcome.Result != GiftCodeResult.Ok ? Rules(engine, c) : null,
            },
        };
        return JsonSerializer.Serialize(state, Options);
    }

    /// <summary>
    /// Writes the answer of a redemption on the website.
    /// </summary>
    /// <param name="engine">The engine.</param>
    /// <param name="outcome">The outcome.</param>
    /// <param name="characterName">The character which gets the zen and items.</param>
    /// <param name="itemName">Gives the name of an item.</param>
    /// <returns>The JSON.</returns>
    public static string WriteRedeemAnswer(GiftCodeEngine engine, GiftCodeOutcome outcome, string characterName, Func<short, short, string?>? itemName = null)
    {
        var answer = new
        {
            v = 1,
            result = outcome.Result.ToString(),
            code = outcome.Entry?.Code,
            character = characterName,
            reward = outcome is { Result: GiftCodeResult.Ok, Code: { } code } ? Reward(code.Reward, itemName) : null,
            triesLeft = outcome.TriesLeft,
            retryAt = outcome.RetryAtUtc is { } retry ? ToMs(retry) : (long?)null,
            details = outcome.Code is { } c && outcome.Result != GiftCodeResult.Ok ? Rules(engine, c) : null,
        };
        return JsonSerializer.Serialize(answer, Options);
    }

    /// <summary>
    /// Writes the public codes for the website.
    /// </summary>
    /// <param name="engine">The engine.</param>
    /// <param name="nowUtc">The time.</param>
    /// <param name="itemName">Gives the name of an item.</param>
    /// <returns>The JSON.</returns>
    public static string WritePublic(GiftCodeEngine engine, DateTime nowUtc, Func<short, short, string?>? itemName = null)
    {
        var codes = engine.Configuration.ShowPublicCodes ? engine.PublicCodes(nowUtc) : new List<GiftCodeDefinition>();
        var state = new
        {
            v = 1,
            enabled = true,
            website = engine.Configuration.AllowWebsite,
            now = ToMs(nowUtc),
            timeZone = engine.Configuration.TimeZone,
            codes = codes.Select(c => new
            {
                code = c.Code,
                note = c.Note,
                reward = Reward(c.Reward, itemName),
                rules = Rules(engine, c),
                usesLeft = c.MaxUses > 0 ? Math.Max(0, c.MaxUses - engine.UsesOf(c.Id)) : (int?)null,
            }),
        };
        return JsonSerializer.Serialize(state, Options);
    }

    private static object Reward(GiftCodeReward reward, Func<short, short, string?>? itemName) => new
    {
        coins = Math.Max(0, reward.Coins),
        coinType = reward.CoinType,
        zen = Math.Max(0, reward.Zen),
        items = reward.Items.Where(i => i.Count > 0).Select(i => Item(i, itemName)),
    };

    private static object Rules(GiftCodeEngine engine, GiftCodeDefinition code) => new
    {
        limit = code.Limit,
        start = engine.StartUtc(code) is { } start ? ToMs(start) : (long?)null,
        end = engine.EndUtc(code) is { } end ? ToMs(end) : (long?)null,
        minLevel = code.MinLevel,
        maxLevel = code.MaxLevel,
        minResets = code.MinResets,
        maxResets = code.MaxResets,
        classes = code.Classes.Select(c => (int)c),
    };

    private static object Item(GiftCodeItem item, Func<short, short, string?>? itemName) => new
    {
        group = item.Group,
        number = item.Number,
        level = item.Level,
        count = item.Count,
        luck = item.Luck,
        skill = item.Skill,
        option = item.OptionLevel,
        excellent = item.ExcellentOptions,
        name = itemName?.Invoke(item.Group, item.Number),
    };

    private static long ToMs(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
}
