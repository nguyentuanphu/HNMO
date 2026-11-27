// <copyright file="WorldBossBoardWriter.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.WorldBosses;

using System.Text.Json;
using System.Text.Json.Serialization;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// Writes the world bosses as JSON: the state of a character for the game client (HnmoWorldBossesState), and the
/// public state for the website (no account data). Non-ASCII text is escaped, so the JSON is plain ASCII.
/// </summary>
public static class WorldBossBoardWriter
{
    /// <summary>The lines of a damage table sent to the game client.</summary>
    public const int TopLines = 5;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Writes the state of a character.
    /// </summary>
    /// <param name="engine">The engine.</param>
    /// <param name="characterId">The character id.</param>
    /// <param name="nowUtc">The time.</param>
    /// <param name="mapName">Gives the name of a map.</param>
    /// <param name="boardEvent">What happened, if anything.</param>
    /// <returns>The JSON.</returns>
    public static string WriteBoard(WorldBossEngine engine, string characterId, DateTime nowUtc, Func<short, string> mapName, WorldBossBoardEvent? boardEvent)
    {
        var alive = engine.AllAlive();
        var bosses = engine.Configuration.EnabledBosses().Select(boss =>
        {
            var current = alive.FirstOrDefault(a => a.BossId == boss.Id);
            object? fight = null;
            if (current is not null)
            {
                var ranked = WorldBossEngine.Rank(current, boss.RankBy);
                var total = Math.Max(1, ranked.Sum(r => r.Entry.Damage));
                var mine = ranked.FirstOrDefault(r => r.Entry.CharacterId == characterId);
                fight = new
                {
                    current.X,
                    current.Y,
                    SpawnedAt = ToMs(current.SpawnedAtUtc),
                    DespawnAt = ToMs(current.DespawnAtUtc),
                    current.MaximumHealth,
                    TotalDamage = ranked.Sum(r => r.Entry.Damage),
                    Fighters = ranked.Count,
                    MyDamage = mine.Entry?.Damage ?? 0,
                    MyRank = mine.Entry is null ? 0 : mine.Rank,
                    Top = ranked.Take(TopLines).Select(r => new { r.Rank, r.Entry.Name, r.Entry.Damage, Share = Math.Round(r.Entry.Damage * 100.0 / total, 1) }).ToList(),
                };
            }

            return new
            {
                boss.Id,
                boss.NameEn,
                boss.NameVi,
                Map = boss.MapNumber,
                MapName = mapName(boss.MapNumber),
                NextSpawn = engine.NextSpawn(boss, nowUtc) is { } next ? ToMs(next) : (long?)null,
                Alive = fight,
            };
        }).ToList();
        var points = engine.PointsOf(WeeklyCategory.BossHunt, characterId, nowUtc);
        var board = new
        {
            V = 1,
            Now = ToMs(nowUtc),
            Bosses = bosses,
            Owed = engine.Owed(characterId).Select(o => new { o.BossEn, o.BossVi, o.Tier, Reward = Reward(o.Reward, o.CoinsGiven) }).ToList(),
            HuntPoints = points.Points,
            HuntRank = points.Rank,
            WeekEnds = ToMs(engine.Clock.NextWeekStart(nowUtc)),
            Event = boardEvent,
        };
        return JsonSerializer.Serialize(board, Options);
    }

    /// <summary>
    /// Writes the public state for the website: each boss with its next spawn, map and rewards, the recent kills with
    /// the top damage, the announcements and the weekly rankings with last week's winners.
    /// </summary>
    /// <param name="engine">The engine.</param>
    /// <param name="nowUtc">The time.</param>
    /// <param name="mapName">Gives the name of a map, if known.</param>
    /// <param name="itemName">Gives the name of a reward item, if known.</param>
    /// <returns>The JSON.</returns>
    public static string WritePublic(WorldBossEngine engine, DateTime nowUtc, Func<short, string?>? mapName = null, Func<short, short, string?>? itemName = null)
    {
        var configuration = engine.Configuration;
        var alive = engine.AllAlive();
        var kills = engine.Kills();
        var rankingCategories = new[] { WeeklyCategory.BossHunt, WeeklyCategory.BloodCastle, WeeklyCategory.DevilSquare, WeeklyCategory.ChaosCastle };
        List<WorldBossAnnouncement> announcements;
        WeeklyResult? lastWeek;
        string? siegeGuild;
        lock (engine.Store.Lock)
        {
            engine.RollWeek(nowUtc);
            announcements = engine.Store.Data.Announcements.Take(8).ToList();
            lastWeek = engine.Store.Data.LastWeek;
            siegeGuild = engine.Store.Data.CastleSiegeGuildName;
        }

        var value = new
        {
            V = 1,
            Now = ToMs(nowUtc),
            configuration.TimeZone,
            Bosses = configuration.EnabledBosses().Select(boss =>
            {
                var current = alive.FirstOrDefault(a => a.BossId == boss.Id);
                var last = kills.FirstOrDefault(k => k.BossId == boss.Id);
                return new
                {
                    boss.Id,
                    boss.NameEn,
                    boss.NameVi,
                    Map = boss.MapNumber,
                    MapName = mapName?.Invoke(boss.MapNumber),
                    Schedule = boss.ScheduleKind == WorldBossScheduleKind.EveryHours ? "every" : "fixed",
                    boss.EveryHours,
                    Times = WorldBossSchedule.FormatTimes(WorldBossSchedule.TimesOfDay(boss)),
                    Days = (boss.Days ?? []).OrderBy(d => ((int)d + 6) % 7).Select(d => d.ToString()).ToList(),
                    NextSpawn = engine.NextSpawn(boss, nowUtc) is { } next ? ToMs(next) : (long?)null,
                    boss.DespawnMinutes,
                    boss.RankBy,
                    boss.HuntPoints,
                    Alive = current is null ? null : new { current.X, current.Y, SpawnedAt = ToMs(current.SpawnedAtUtc), DespawnAt = ToMs(current.DespawnAtUtc), Fighters = current.Damage.Count },
                    Rewards = new
                    {
                        Top1 = Reward(boss.Top1, false, itemName),
                        Top2 = Reward(boss.Top2, false, itemName),
                        Top3 = Reward(boss.Top3, false, itemName),
                        Top10 = Reward(boss.Top10, false, itemName),
                        Participation = Reward(boss.Participation, false, itemName),
                        LastHit = Reward(boss.LastHit, false, itemName),
                        boss.ParticipationMinimumPercent,
                        boss.ParticipationMinimumDamage,
                    },
                    LastKill = last is null ? null : Kill(last, 5),
                };
            }).ToList(),
            Kills = kills.Take(5).Select(k => Kill(k, 5)).ToList(),
            Announcements = announcements.Select(a => new { At = ToMs(a.AtUtc), a.BossId, a.Phase, a.TextEn, a.TextVi }).ToList(),
            Week = new
            {
                Start = engine.Clock.WeekKey(nowUtc),
                Ends = ToMs(engine.Clock.NextWeekStart(nowUtc)),
                Rankings = rankingCategories.ToDictionary(
                    c => c.ToString(),
                    c => engine.Ranking(c, nowUtc).Select(r => new { r.Rank, r.Entry.Name, r.Entry.Points }).ToList()),
                CastleSiegeGuild = siegeGuild,
            },
            Prizes = rankingCategories.ToDictionary(c => c.ToString(), c =>
            {
                var ranking = configuration.Ranking(c)!;
                return (object)new { ranking.Enabled, ranking.CoinType, Ranks = ranking.Ranks.OrderBy(r => r.Rank).Select(r => new { r.Rank, r.Coins }).ToList(), ranking.PointsFinish, ranking.PointsWin };
            }),
            CastleSiege = new { configuration.CastleSiege.Enabled, configuration.CastleSiege.CoinType, configuration.CastleSiege.CoinsPerMember, configuration.CastleSiege.GuildMasterBonus },
            LastWeek = lastWeek is null ? null : new
            {
                lastWeek.Week,
                Winners = lastWeek.Winners.ToDictionary(w => w.Key.ToString(), w => w.Value.Select(x => new { x.Rank, x.Name, x.Points, x.Coins, x.CoinType }).ToList()),
                CastleSiegeGuild = lastWeek.CastleSiegeGuild,
                CastleSiegeMembers = lastWeek.CastleSiegeMembers.Count,
            },
        };
        return JsonSerializer.Serialize(value, Options);
    }

    private static object Kill(WorldBossKillRecord record, int lines) => new
    {
        record.InstanceId,
        record.BossId,
        record.NameEn,
        record.NameVi,
        Map = record.MapNumber,
        SpawnedAt = ToMs(record.SpawnedAtUtc),
        EndedAt = ToMs(record.EndedAtUtc),
        record.Killed,
        record.LastHit,
        record.TotalDamage,
        Fighters = record.Lines.Count,
        Top = record.Lines.Take(lines).Select(l => new { l.Rank, l.Name, l.Damage, l.Share }).ToList(),
    };

    private static object Reward(MissionReward reward, bool coinsGiven, Func<short, short, string?>? itemName = null) => new
    {
        reward.Zen,
        reward.Coins,
        reward.CoinType,
        CoinsGiven = coinsGiven ? true : (bool?)null,
        Items = reward.Items.Where(i => i.Count > 0).Select(i => new { i.Group, i.Number, i.Level, i.Count, Name = itemName?.Invoke(i.Group, i.Number) }).ToList(),
    };

    private static long ToMs(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
}
