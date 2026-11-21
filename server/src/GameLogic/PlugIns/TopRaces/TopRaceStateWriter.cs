// <copyright file="TopRaceStateWriter.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TopRaces;

using System.Text.Json;
using System.Text.Json.Serialization;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// Writes the top races as JSON: the state of a player for the game client (HnmoTopRacesState), and the public state
/// for the website (no account data). Non-ASCII text is escaped, so the JSON is plain ASCII.
/// </summary>
public static class TopRaceStateWriter
{
    /// <summary>The finished races sent to the game client.</summary>
    public const int ClientHistory = 6;

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
    /// <param name="characterId">The character id.</param>
    /// <param name="characterClass">The character's class (<see cref="TopRaceClass"/>).</param>
    /// <param name="guildKey">The key of the character's guild (<see cref="TopRaceEngine.GuildKey"/>); <c>null</c> without one.</param>
    /// <param name="nowUtc">The time.</param>
    /// <param name="stateEvent">What happened, if anything.</param>
    /// <returns>The JSON.</returns>
    public static string WriteState(TopRaceEngine engine, string characterId, int characterClass, string? guildKey, DateTime nowUtc, TopRaceStateEvent? stateEvent)
    {
        var running = engine.Running(nowUtc);
        var state = new
        {
            v = 1,
            now = ToMs(nowUtc),
            myClass = characterClass,
            races = running.Select(r =>
            {
                var key = r.Race.IsGuildRace ? guildKey : characterId;
                var (rank, value) = key is null ? (0, 0L) : engine.RankOf(r.Race, r.Run, key);
                var table = TopRaceEngine.TableOf(r.Race, characterClass);
                var eligible = r.Race.IsGuildRace ? guildKey is not null : r.Race.Scope != TopRaceScope.OneClass || (int)r.Race.Class == characterClass;
                return new
                {
                    race = Race(r.Race, null),
                    start = ToMs(r.Run.StartUtc),
                    end = ToMs(r.Run.EndUtc),
                    eligible,
                    table,
                    myRank = rank,
                    myValue = value,
                    top = engine.Standings(r.Race, r.Run, table).Select(Line),
                };
            }),
            upcoming = engine.Upcoming(nowUtc).Select(u => new { race = Race(u.Race, null), start = ToMs(u.Window.StartUtc), end = ToMs(u.Window.EndUtc) }),
            history = engine.History(nowUtc, ClientHistory).Select(h => Result(h, null)),
            owed = engine.Owed(characterId).Select(o => new
            {
                raceId = o.RaceId, titleEn = o.TitleEn, titleVi = o.TitleVi, period = o.PeriodKey, rank = o.Rank,
                reward = new { zen = o.Reward.Zen, items = o.Reward.Items.Select(i => Item(i, null)) },
            }),
            @event = stateEvent is null ? null : new
            {
                type = stateEvent.Type, result = stateEvent.Result, raceId = stateEvent.RaceId, textEn = stateEvent.TextEn, textVi = stateEvent.TextVi,
                rank = stateEvent.Rank, coins = stateEvent.Coins, inGame = stateEvent.InGame,
            },
        };
        return JsonSerializer.Serialize(state, Options);
    }

    /// <summary>
    /// Writes the public state for the website: the running races with their tables, the races to come and the past
    /// winners.
    /// </summary>
    /// <param name="engine">The engine.</param>
    /// <param name="nowUtc">The time.</param>
    /// <param name="itemName">Gives the name of an item.</param>
    /// <returns>The JSON.</returns>
    public static string WritePublic(TopRaceEngine engine, DateTime nowUtc, Func<short, short, string?>? itemName = null)
    {
        var state = new
        {
            v = 1,
            enabled = true,
            now = ToMs(nowUtc),
            timeZone = engine.Configuration.TimeZone,
            races = engine.Running(nowUtc).Select(r => new
            {
                race = Race(r.Race, itemName),
                start = ToMs(r.Run.StartUtc),
                end = ToMs(r.Run.EndUtc),
                tables = TopRaceEngine.TableClasses(r.Race)
                    .Select(t => new { cls = t, lines = engine.Standings(r.Race, r.Run, t).Select(Line).ToList() })
                    .Where(t => t.lines.Count > 0 || r.Race.Scope != TopRaceScope.PerClass),
            }),
            upcoming = engine.Upcoming(nowUtc).Select(u => new { race = Race(u.Race, itemName), start = ToMs(u.Window.StartUtc), end = ToMs(u.Window.EndUtc) }),
            history = engine.History(nowUtc, 20).Select(h => Result(h, itemName)),
        };
        return JsonSerializer.Serialize(state, Options);
    }

    private static object Race(TopRaceDefinition race, Func<short, short, string?>? itemName) => new
    {
        id = race.Id,
        titleEn = race.TitleEn,
        titleVi = race.TitleViOrEn,
        kind = race.Kind,
        @event = race.Event,
        scope = race.IsGuildRace ? TopRaceScope.AllClasses : race.Scope,
        cls = (int)race.Class,
        period = race.Period,
        minimum = Math.Max(1, race.Minimum),
        prizes = race.Prizes.Where(p => !p.IsEmpty).OrderBy(p => p.RankFrom).Select(p => new
        {
            from = p.RankFrom,
            to = Math.Max(p.RankFrom, p.RankTo),
            coins = p.Coins,
            coinType = p.CoinType,
            zen = p.Zen,
            items = p.Items.Where(i => i.Count > 0).Select(i => Item(i, itemName)),
        }),
    };

    private static object Line(TopRaceStanding standing) =>
        new { rank = standing.Rank, name = standing.Entry.Name, value = standing.Entry.Value, cls = standing.Entry.Class };

    private static object Result(TopRaceResult result, Func<short, short, string?>? itemName) => new
    {
        raceId = result.RaceId,
        titleEn = result.TitleEn,
        titleVi = result.TitleVi,
        kind = result.Kind,
        period = result.Period,
        periodKey = result.PeriodKey,
        start = ToMs(result.StartUtc),
        end = ToMs(result.EndUtc),
        paid = result.Paid,
        tables = result.Tables.Select(t => new
        {
            cls = t.Class,
            lines = t.Lines.Take(TopRaceEngine.KeptRanks).Select(l => new
            {
                rank = l.Rank, name = l.Name, value = l.Value, cls = l.Class, prized = l.Prized, coins = l.Coins, coinType = l.CoinType, zen = l.Zen,
                items = l.Items.Select(i => Item(i, itemName)),
            }),
        }),
    };

    private static object Item(MissionItemReward item, Func<short, short, string?>? itemName) =>
        new { group = item.Group, number = item.Number, level = item.Level, count = item.Count, name = itemName?.Invoke(item.Group, item.Number) };

    private static long ToMs(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
}
