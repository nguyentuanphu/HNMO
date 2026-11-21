// <copyright file="GuildStateWriter.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Guilds;

using System.Text.Json;
using System.Text.Json.Serialization;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// Writes the guild levels as JSON: the state of a member for the game client (HnmoGuildState), and the public state
/// for the website (no account data). Non-ASCII text is escaped, so the JSON is plain ASCII.
/// </summary>
public static class GuildStateWriter
{
    /// <summary>The ladder lines sent to the game client.</summary>
    public const int ClientLadderLines = 5;

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
    /// <param name="guild">The player's guild; <c>null</c> without one.</param>
    /// <param name="characterId">The character id.</param>
    /// <param name="memberName">The character name.</param>
    /// <param name="nowUtc">The time.</param>
    /// <param name="stateEvent">What happened, if anything.</param>
    /// <returns>The JSON.</returns>
    public static string WriteState(GuildEngine engine, GuildKey? guild, string characterId, string memberName, DateTime nowUtc, GuildStateEvent? stateEvent)
    {
        var configuration = engine.Configuration;
        var progress = guild is null ? null : engine.Progress(guild.GuildId, nowUtc);
        var level = progress?.Level ?? 1;
        var current = engine.Level(level);
        var next = engine.NextLevel(level);
        var buff = engine.BuffOf(level);
        var nextBuff = next is { HasBuff: true } && configuration.BuffsEnabled ? next : null;
        var state = new
        {
            v = 1,
            now = ToMs(nowUtc),
            inGuild = guild is not null,
            guildName = guild?.Name,
            level,
            maxLevel = engine.Levels.Count == 0 ? 1 : engine.Levels.Max(l => l.Level),
            xp = progress?.Xp ?? 0,
            levelXp = current?.Xp ?? 0,
            nextXp = next?.Xp,
            weekXp = progress?.WeekXp ?? 0,
            rank = guild is null ? 0 : engine.RankOf(guild.GuildId, nowUtc),
            myWeekXp = progress?.WeekMembers.GetValueOrDefault(memberName) ?? 0,
            today = engine.Today(characterId, nowUtc),
            dailyCap = configuration.DailyCapPerMember,
            weekEnds = ToMs(engine.Clock.NextWeekStart(nowUtc)),
            buffNameEn = configuration.BuffNameEn,
            buffNameVi = string.IsNullOrWhiteSpace(configuration.BuffNameVi) ? configuration.BuffNameEn : configuration.BuffNameVi,
            buff = buff is null ? null : Buff(buff),
            nextBuff = nextBuff is null ? null : new { level = nextBuff.Level, exp = nextBuff.ExperiencePercent, zen = nextBuff.ZenPercent, defense = nextBuff.Defense },
            ladderOn = configuration.LadderEnabled,
            ladder = engine.Ladder(nowUtc, ClientLadderLines).Select(e => new { rank = e.Rank, name = e.Progress.Name, xp = e.Progress.WeekXp, level = engine.LevelOf(e.Progress.Xp) }),
            rewards = configuration.LadderEnabled ? configuration.LadderRewards.OrderBy(r => r.RankFrom).Select(Reward) : [],
            owed = engine.Owed(characterId).Select(o => new { week = o.Week, rank = o.Rank, reward = new { zen = o.Reward.Zen, items = o.Reward.Items.Select(Item) } }),
            @event = stateEvent is null ? null : new
            {
                type = stateEvent.Type, result = stateEvent.Result, level = stateEvent.Level, rank = stateEvent.Rank, coins = stateEvent.Coins,
                week = stateEvent.Week, inGame = stateEvent.InGame,
            },
        };
        return JsonSerializer.Serialize(state, Options);
    }

    /// <summary>
    /// Writes the public state for the website: this week's ladder, last week's winners, the guilds with their level and
    /// members, the castle siege results, the levels with their buffs and the ladder rewards.
    /// </summary>
    /// <param name="engine">The engine.</param>
    /// <param name="nowUtc">The time.</param>
    /// <param name="existing">The guilds which exist, with their member count; others (disbanded) are left out. <c>null</c> keeps all, without counts.</param>
    /// <param name="itemName">Gives the name of an item.</param>
    /// <returns>The JSON.</returns>
    public static string WritePublic(GuildEngine engine, DateTime nowUtc, IReadOnlyDictionary<Guid, int>? existing, Func<short, short, string?>? itemName = null)
    {
        var configuration = engine.Configuration;
        bool Exists(Guid id) => existing is null || existing.ContainsKey(id);
        int? Members(Guid id) => existing is not null && existing.TryGetValue(id, out var count) ? count : null;
        var last = engine.LastWeek(nowUtc);
        var state = new
        {
            v = 1,
            enabled = true,
            now = ToMs(nowUtc),
            weekEnds = ToMs(engine.Clock.NextWeekStart(nowUtc)),
            dailyCap = configuration.DailyCapPerMember,
            buffsEnabled = configuration.BuffsEnabled,
            buffNameEn = configuration.BuffNameEn,
            buffNameVi = string.IsNullOrWhiteSpace(configuration.BuffNameVi) ? configuration.BuffNameEn : configuration.BuffNameVi,
            levels = engine.Levels.Select(l => new { level = l.Level, xp = l.Xp, exp = l.ExperiencePercent, zen = l.ZenPercent, defense = l.Defense }),
            sources = Enum.GetValues<GuildXpSource>().Where(s => engine.XpFor(s) > 0).Select(s => new { source = s, xp = engine.XpFor(s) }),
            onlineMinutes = configuration.OnlineMinutes,
            siegeLadderBonus = configuration.CastleSiegeLadderBonus,
            ladderOn = configuration.LadderEnabled,
            ladderMinimumXp = configuration.LadderMinimumXp,
            rewards = configuration.LadderEnabled ? configuration.LadderRewards.OrderBy(r => r.RankFrom).Select(r => Reward(r, itemName)) : [],
            ladder = engine.Ladder(nowUtc, int.MaxValue).Where(e => Exists(e.GuildId)).Take(GuildEngine.KeptRanks)
                .Select((e, i) => new { rank = i + 1, name = e.Progress.Name, xp = e.Progress.WeekXp, level = engine.LevelOf(e.Progress.Xp), members = Members(e.GuildId) }),
            lastWeek = last is null ? null : new
            {
                week = last.Week,
                lines = last.Lines.Select(l => new { rank = l.Rank, name = l.Name, xp = l.Xp, coins = l.CoinsPerMember, coinType = l.CoinType, paid = l.PaidMembers }),
            },
            guilds = engine.AllGuilds(nowUtc).Where(g => Exists(g.GuildId)).Take(50)
                .Select(g => new { name = g.Progress.Name, level = engine.LevelOf(g.Progress.Xp), xp = g.Progress.Xp, members = Members(g.GuildId) }),
            sieges = engine.Sieges().Select(s => new { at = ToMs(s.AtUtc), name = s.Name }),
        };
        return JsonSerializer.Serialize(state, Options);
    }

    private static object Buff(GuildLevelDefinition level) => new { level = level.Level, exp = level.ExperiencePercent, zen = level.ZenPercent, defense = level.Defense };

    private static object Reward(GuildLadderReward reward) => Reward(reward, null);

    private static object Reward(GuildLadderReward reward, Func<short, short, string?>? itemName) => new
    {
        from = reward.RankFrom,
        to = Math.Max(reward.RankFrom, reward.RankTo),
        coins = reward.CoinsPerMember,
        masterBonus = reward.MasterBonusCoins,
        coinType = reward.CoinType,
        zen = reward.ZenPerMember,
        items = reward.Items.Where(i => i.Count > 0).Select(i => Item(i, itemName)),
    };

    private static object Item(MissionItemReward item) => Item(item, null);

    private static object Item(MissionItemReward item, Func<short, short, string?>? itemName) =>
        new { group = item.Group, number = item.Number, level = item.Level, count = item.Count, name = itemName?.Invoke(item.Group, item.Number) };

    private static long ToMs(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
}
