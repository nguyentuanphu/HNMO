// <copyright file="WorldBossEngine.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.WorldBosses;

using System.Globalization;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// What the plug-in has to do for a world boss now.
/// </summary>
public enum WorldBossActionKind
{
    /// <summary>Announce the coming spawn.</summary>
    Soon,

    /// <summary>Spawn the boss.</summary>
    Spawn,

    /// <summary>The boss leaves: its time is over (or it was switched off).</summary>
    Despawn,
}

/// <summary>
/// The rules of the world bosses, without the game: the schedule, the damage table, the rewards by rank, the
/// boss-hunt points and the weekly rankings (Monday 00:00 in the configured time zone) which pay once per week.
/// The state is in the <see cref="WorldBossStore"/>; every method takes its lock.
/// </summary>
public sealed class WorldBossEngine
{
    /// <summary>The kills and escapes which are kept.</summary>
    public const int KeptKills = 30;

    /// <summary>The announcements which are kept.</summary>
    public const int KeptAnnouncements = 20;

    /// <summary>The lines of a kill's damage table which are kept.</summary>
    public const int KeptLines = 50;

    /// <summary>The weekly ranks which are paid.</summary>
    public const int PaidRanks = 10;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldBossEngine"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="store">The store.</param>
    public WorldBossEngine(WorldBossesConfiguration configuration, WorldBossStore store)
    {
        this.Configuration = configuration;
        this.Store = store;
        this.Clock = new MissionClock(configuration.TimeZone, 0);
    }

    /// <summary>Gets the configuration.</summary>
    public WorldBossesConfiguration Configuration { get; }

    /// <summary>Gets the store.</summary>
    public WorldBossStore Store { get; }

    /// <summary>Gets the clock of the weeks.</summary>
    public MissionClock Clock { get; }

    /// <summary>Gets the time zone of the schedules.</summary>
    public TimeZoneInfo TimeZone => this.Clock.TimeZone;

    /// <summary>
    /// Gets the reward of a tier.
    /// </summary>
    /// <param name="boss">The boss.</param>
    /// <param name="tier">The tier: "top1", "top2", "top3", "top10", "participation" or "lasthit".</param>
    /// <returns>The reward.</returns>
    public static MissionReward RewardOf(WorldBossDefinition boss, string tier) => tier switch
    {
        "top1" => boss.Top1,
        "top2" => boss.Top2,
        "top3" => boss.Top3,
        "top10" => boss.Top10,
        "participation" => boss.Participation,
        _ => boss.LastHit,
    };

    /// <summary>
    /// Gets the rank tier of a rank.
    /// </summary>
    /// <param name="rank">The rank (1-based).</param>
    /// <returns>The tier, or <c>null</c> beyond rank 10.</returns>
    public static string? RankTier(int rank) => rank switch
    {
        1 => "top1",
        2 => "top2",
        3 => "top3",
        <= 10 and > 0 => "top10",
        _ => null,
    };

    /// <summary>
    /// Fills a text of a boss.
    /// </summary>
    /// <param name="template">The text with {boss}, {map}, {minutes}, {x}, {y}, {killer} and {top}.</param>
    /// <param name="boss">The boss name.</param>
    /// <param name="map">The map name.</param>
    /// <param name="values">Other values by name.</param>
    /// <returns>The text.</returns>
    public static string Fill(string? template, string boss, string map, IReadOnlyDictionary<string, string>? values = null)
    {
        var text = (template ?? string.Empty).Replace("{boss}", boss, StringComparison.Ordinal).Replace("{map}", map, StringComparison.Ordinal);
        foreach (var (key, value) in values ?? new Dictionary<string, string>())
        {
            text = text.Replace("{" + key + "}", value, StringComparison.Ordinal);
        }

        return text;
    }

    /// <summary>
    /// Gets the damage table of an appearance, ranked as the rewards will be.
    /// </summary>
    /// <param name="alive">The appearance.</param>
    /// <param name="rankBy">What the ranking counts.</param>
    /// <returns>The lines, best first: the entry, its rank and the damage which ranks it (the party's in party mode).</returns>
    public static List<(WorldBossDamageEntry Entry, int Rank, long RankDamage)> Rank(WorldBossAliveState alive, WorldBossRankBy rankBy)
    {
        var entries = alive.Damage.Values.Where(e => e.Damage > 0).ToList();
        var groups = entries
            .GroupBy(e => rankBy == WorldBossRankBy.Party && !string.IsNullOrEmpty(e.Party) ? "party:" + e.Party : "player:" + e.CharacterId)
            .Select(g => (Members: g.OrderByDescending(e => e.Damage).ThenBy(e => e.Name, StringComparer.Ordinal).ToList(), Damage: g.Sum(e => e.Damage)))
            .OrderByDescending(g => g.Damage)
            .ThenBy(g => g.Members[0].Name, StringComparer.Ordinal)
            .ToList();
        var result = new List<(WorldBossDamageEntry Entry, int Rank, long RankDamage)>();
        for (var i = 0; i < groups.Count; i++)
        {
            result.AddRange(groups[i].Members.Select(m => (m, i + 1, groups[i].Damage)));
        }

        return result;
    }

    /// <summary>
    /// Gets the coin grants of a finished week's rankings (not the castle siege), each with a unique reference.
    /// </summary>
    /// <param name="result">The week.</param>
    /// <returns>The grants.</returns>
    public static List<CoinGrant> RankingGrants(WeeklyResult result) =>
        result.Winners
            .SelectMany(w => w.Value.Where(winner => winner.Coins > 0 && winner.Login.Length > 0).Select(winner => new CoinGrant(
                winner.Login,
                winner.CoinType,
                winner.Coins,
                string.Create(CultureInfo.InvariantCulture, $"Weekly ranking {w.Key}, week of {result.Week}: rank {winner.Rank} ({winner.Name})"),
                $"hnmo-week:{result.Week}:{w.Key}:{winner.CharacterId}")))
            .ToList();

    /// <summary>
    /// Gets the boss with an id.
    /// </summary>
    /// <param name="bossId">The id.</param>
    /// <returns>The boss, or <c>null</c>.</returns>
    public WorldBossDefinition? Boss(string bossId) => this.Configuration.Bosses.FirstOrDefault(b => string.Equals(b.Id, bossId, StringComparison.Ordinal));

    /// <summary>
    /// Gets the next spawn time of a boss (computed and kept when it's new or the schedule changed).
    /// </summary>
    /// <param name="boss">The boss.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The next spawn, or <c>null</c>.</returns>
    public DateTime? NextSpawn(WorldBossDefinition boss, DateTime nowUtc)
    {
        lock (this.Store.Lock)
        {
            return this.StateOf(boss, nowUtc).NextSpawnUtc;
        }
    }

    /// <summary>
    /// Plans what to do now for the bosses which this game server hosts: announcements, spawns and despawns.
    /// It moves the schedule on as it plans, so each action comes once.
    /// </summary>
    /// <param name="hosted">The bosses which this game server hosts.</param>
    /// <param name="serverId">The game server.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The actions.</returns>
    public List<WorldBossAction> Plan(IEnumerable<WorldBossDefinition> hosted, byte serverId, DateTime nowUtc)
    {
        var actions = new List<WorldBossAction>();
        lock (this.Store.Lock)
        {
            var data = this.Store.Data;
            foreach (var alive in data.Alive.Where(a => a.ServerId == serverId).ToList())
            {
                var boss = this.Boss(alive.BossId);
                if (boss is null || !boss.Enabled || nowUtc >= alive.DespawnAtUtc)
                {
                    actions.Add(new WorldBossAction(WorldBossActionKind.Despawn, boss, alive.DespawnAtUtc, alive.InstanceId));
                }
            }

            foreach (var boss in hosted.Where(b => b.Enabled && !string.IsNullOrWhiteSpace(b.Id)))
            {
                var state = this.StateOf(boss, nowUtc);
                if (state.NextSpawnUtc is not { } next)
                {
                    continue;
                }

                if (nowUtc >= next)
                {
                    var isAlive = data.Alive.Any(a => a.BossId == boss.Id);
                    if (!isAlive && nowUtc - next < TimeSpan.FromMinutes(Math.Max(1, boss.DespawnMinutes)))
                    {
                        actions.Add(new WorldBossAction(WorldBossActionKind.Spawn, boss, next));
                    }

                    state.NextSpawnUtc = WorldBossSchedule.Next(boss, this.TimeZone, nowUtc);
                    this.Store.Changed();
                    continue;
                }

                if (boss.AnnounceMinutesBefore > 0 && nowUtc >= next.AddMinutes(-boss.AnnounceMinutesBefore) && state.AnnouncedUtc != next)
                {
                    state.AnnouncedUtc = next;
                    this.Store.Changed();
                    actions.Add(new WorldBossAction(WorldBossActionKind.Soon, boss, next));
                }
            }
        }

        return actions;
    }

    /// <summary>
    /// Keeps a boss which appeared on a map.
    /// </summary>
    /// <param name="boss">The boss.</param>
    /// <param name="serverId">The game server.</param>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <param name="maximumHealth">Its maximum health.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The state of the appearance.</returns>
    public WorldBossAliveState RegisterSpawn(WorldBossDefinition boss, byte serverId, byte x, byte y, long maximumHealth, DateTime nowUtc)
    {
        var alive = new WorldBossAliveState
        {
            BossId = boss.Id,
            ServerId = serverId,
            InstanceId = string.Create(CultureInfo.InvariantCulture, $"{boss.Id}-{nowUtc:yyyyMMddHHmmss}-{serverId}"),
            MapNumber = boss.MapNumber,
            X = x,
            Y = y,
            SpawnedAtUtc = nowUtc,
            DespawnAtUtc = nowUtc.AddMinutes(Math.Max(1, boss.DespawnMinutes)),
            MaximumHealth = Math.Max(1, maximumHealth),
        };
        lock (this.Store.Lock)
        {
            this.Store.Data.Alive.Add(alive);
            this.Store.Changed();
        }

        return alive;
    }

    /// <summary>
    /// Gets an appearance which is alive.
    /// </summary>
    /// <param name="instanceId">The appearance.</param>
    /// <returns>A copy of its state, or <c>null</c>.</returns>
    public WorldBossAliveState? Alive(string instanceId)
    {
        lock (this.Store.Lock)
        {
            return this.Store.Data.Alive.FirstOrDefault(a => a.InstanceId == instanceId) is { } alive ? Copy(alive) : null;
        }
    }

    /// <summary>
    /// Gets the appearances which are alive.
    /// </summary>
    /// <returns>Copies of their states.</returns>
    public List<WorldBossAliveState> AllAlive()
    {
        lock (this.Store.Lock)
        {
            return this.Store.Data.Alive.Select(Copy).ToList();
        }
    }

    /// <summary>
    /// Adds damage of a player to an appearance (at most its maximum health per hit).
    /// </summary>
    /// <param name="instanceId">The appearance.</param>
    /// <param name="characterId">The character id.</param>
    /// <param name="name">The character name.</param>
    /// <param name="login">The login name.</param>
    /// <param name="party">The party key, if any.</param>
    /// <param name="damage">The damage.</param>
    /// <returns><c>true</c>, if the appearance is alive.</returns>
    public bool RecordDamage(string instanceId, string characterId, string name, string login, string? party, long damage)
    {
        if (damage <= 0)
        {
            return false;
        }

        lock (this.Store.Lock)
        {
            if (this.Store.Data.Alive.FirstOrDefault(a => a.InstanceId == instanceId) is not { } alive)
            {
                return false;
            }

            if (!alive.Damage.TryGetValue(characterId, out var entry))
            {
                entry = new WorldBossDamageEntry { CharacterId = characterId };
                alive.Damage[characterId] = entry;
            }

            entry.Name = name;
            entry.Login = login;
            entry.Party = party;
            entry.Damage += Math.Min(damage, alive.MaximumHealth);
            this.Store.Changed();
            return true;
        }
    }

    /// <summary>
    /// Ends an appearance with a kill: ranks the damage, gives the boss-hunt points and keeps the earned rewards
    /// (players without damage get nothing).
    /// </summary>
    /// <param name="instanceId">The appearance.</param>
    /// <param name="lastHitCharacterId">The character with the last hit, if it was a player.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The outcome; <c>null</c>, if the appearance isn't alive (any more).</returns>
    public WorldBossKillOutcome? Kill(string instanceId, string? lastHitCharacterId, DateTime nowUtc)
    {
        lock (this.Store.Lock)
        {
            var data = this.Store.Data;
            if (data.Alive.FirstOrDefault(a => a.InstanceId == instanceId) is not { } alive)
            {
                return null;
            }

            data.Alive.Remove(alive);
            var boss = this.Boss(alive.BossId) ?? new WorldBossDefinition { Id = alive.BossId, NameEn = alive.BossId, NameVi = alive.BossId };
            var ranked = Rank(alive, boss.RankBy);
            var total = ranked.Sum(r => r.Entry.Damage);
            var record = NewRecord(alive, boss, nowUtc, true, total);
            var rewards = new Dictionary<string, List<WorldBossOwedReward>>(StringComparer.Ordinal);
            var lastHit = lastHitCharacterId is not null && alive.Damage.TryGetValue(lastHitCharacterId, out var hitter) && hitter.Damage > 0 ? hitter : null;
            record.LastHit = lastHit?.Name;
            this.RollWeek(nowUtc);

            foreach (var (entry, rank, _) in ranked)
            {
                var share = total > 0 ? entry.Damage * 100.0 / total : 0;
                var tiers = new List<string>();
                if (RankTier(rank) is { } rankTier)
                {
                    tiers.Add(rankTier);
                }

                var qualifies = share >= boss.ParticipationMinimumPercent || (boss.ParticipationMinimumDamage > 0 && entry.Damage >= boss.ParticipationMinimumDamage);
                if (qualifies && (rank > PaidRanks || boss.TopAlsoGetParticipation))
                {
                    tiers.Add("participation");
                }

                if (lastHit == entry)
                {
                    tiers.Add("lasthit");
                }

                var points = boss.HuntPoints > 0 ? Math.Max(1, (int)Math.Round(boss.HuntPoints * share / 100.0)) : 0;
                if (points > 0)
                {
                    this.AddPointsLocked(WeeklyCategory.BossHunt, entry.CharacterId, entry.Name, entry.Login, points, nowUtc);
                }

                var earned = new List<WorldBossOwedReward>();
                foreach (var tier in tiers)
                {
                    var reward = RewardOf(boss, tier);
                    if (reward.IsEmpty)
                    {
                        continue;
                    }

                    earned.Add(new WorldBossOwedReward
                    {
                        Reference = $"hnmo-boss:{alive.InstanceId}:{entry.CharacterId}:{tier}",
                        Reason = string.Create(CultureInfo.InvariantCulture, $"World boss {boss.NameEn}: {TierCaption(tier, rank)}"),
                        BossEn = boss.NameEn,
                        BossVi = boss.NameVi,
                        Tier = tier,
                        Login = entry.Login,
                        Reward = CopyReward(reward),
                        EarnedAtUtc = nowUtc,
                    });
                }

                if (earned.Count > 0)
                {
                    rewards[entry.CharacterId] = earned;
                    if (!data.Owed.TryGetValue(entry.CharacterId, out var owed))
                    {
                        owed = new List<WorldBossOwedReward>();
                        data.Owed[entry.CharacterId] = owed;
                    }

                    owed.AddRange(earned.Where(e => owed.All(o => o.Reference != e.Reference)));
                }

                if (record.Lines.Count < KeptLines)
                {
                    record.Lines.Add(new WorldBossKillLine { Rank = rank, Name = entry.Name, Party = entry.Party, Damage = entry.Damage, Share = Math.Round(share, 2), Points = points, Tiers = tiers });
                }
            }

            this.Keep(record);
            this.Store.Changed();
            return new WorldBossKillOutcome(record, rewards);
        }
    }

    /// <summary>
    /// Ends an appearance without a kill (its time is over, or it was switched off): no rewards.
    /// </summary>
    /// <param name="instanceId">The appearance.</param>
    /// <param name="nowUtc">The time.</param>
    /// <param name="keep">Whether it's kept in the list of kills (not for one lost by a restart of the server).</param>
    /// <returns>The record; <c>null</c>, if it wasn't alive.</returns>
    public WorldBossKillRecord? Escape(string instanceId, DateTime nowUtc, bool keep = true)
    {
        lock (this.Store.Lock)
        {
            var data = this.Store.Data;
            if (data.Alive.FirstOrDefault(a => a.InstanceId == instanceId) is not { } alive)
            {
                return null;
            }

            data.Alive.Remove(alive);
            var boss = this.Boss(alive.BossId) ?? new WorldBossDefinition { Id = alive.BossId, NameEn = alive.BossId, NameVi = alive.BossId };
            var ranked = Rank(alive, boss.RankBy);
            var total = ranked.Sum(r => r.Entry.Damage);
            var record = NewRecord(alive, boss, nowUtc, false, total);
            record.Lines.AddRange(ranked.Take(KeptLines).Select(r => new WorldBossKillLine { Rank = r.Rank, Name = r.Entry.Name, Party = r.Entry.Party, Damage = r.Entry.Damage, Share = total > 0 ? Math.Round(r.Entry.Damage * 100.0 / total, 2) : 0 }));
            if (keep)
            {
                this.Keep(record);
            }

            this.Store.Changed();
            return record;
        }
    }

    /// <summary>
    /// Keeps an announcement for the website.
    /// </summary>
    /// <param name="announcement">The announcement.</param>
    public void AddAnnouncement(WorldBossAnnouncement announcement)
    {
        lock (this.Store.Lock)
        {
            var list = this.Store.Data.Announcements;
            list.Insert(0, announcement);
            if (list.Count > KeptAnnouncements)
            {
                list.RemoveRange(KeptAnnouncements, list.Count - KeptAnnouncements);
            }

            this.Store.Changed();
        }
    }

    /// <summary>
    /// Gets the recent kills and escapes.
    /// </summary>
    /// <returns>The records, newest first.</returns>
    public List<WorldBossKillRecord> Kills()
    {
        lock (this.Store.Lock)
        {
            return this.Store.Data.Kills.ToList();
        }
    }

    /// <summary>
    /// Gets the rewards which wait for a character.
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <returns>The rewards.</returns>
    public List<WorldBossOwedReward> Owed(string characterId)
    {
        lock (this.Store.Lock)
        {
            return this.Store.Data.Owed.TryGetValue(characterId, out var owed) ? owed.ToList() : new List<WorldBossOwedReward>();
        }
    }

    /// <summary>
    /// Takes the waiting rewards of a character to give them; what can't be given goes back with <see cref="ReturnOwed"/>.
    /// Two callers never get the same reward.
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <returns>The rewards.</returns>
    public List<WorldBossOwedReward> TakeOwed(string characterId)
    {
        lock (this.Store.Lock)
        {
            if (!this.Store.Data.Owed.Remove(characterId, out var owed))
            {
                return new List<WorldBossOwedReward>();
            }

            this.Store.Changed();
            return owed;
        }
    }

    /// <summary>
    /// Puts rewards back which couldn't be given (e.g. the bag is full).
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <param name="rewards">The rewards.</param>
    public void ReturnOwed(string characterId, IEnumerable<WorldBossOwedReward> rewards)
    {
        var list = rewards.ToList();
        if (list.Count == 0)
        {
            return;
        }

        lock (this.Store.Lock)
        {
            if (!this.Store.Data.Owed.TryGetValue(characterId, out var owed))
            {
                owed = new List<WorldBossOwedReward>();
                this.Store.Data.Owed[characterId] = owed;
            }

            owed.InsertRange(0, list.Where(r => owed.All(o => o.Reference != r.Reference)));
            this.Store.Changed();
        }
    }

    /// <summary>
    /// Adds points of a character to a weekly ranking.
    /// </summary>
    /// <param name="category">The ranking.</param>
    /// <param name="characterId">The character id.</param>
    /// <param name="name">The character name.</param>
    /// <param name="login">The login name.</param>
    /// <param name="points">The points.</param>
    /// <param name="nowUtc">The time.</param>
    public void AddPoints(WeeklyCategory category, string characterId, string name, string login, long points, DateTime nowUtc)
    {
        if (points <= 0)
        {
            return;
        }

        lock (this.Store.Lock)
        {
            this.RollWeek(nowUtc);
            this.AddPointsLocked(category, characterId, name, login, points, nowUtc);
            this.Store.Changed();
        }
    }

    /// <summary>
    /// Keeps the guild which won the castle siege this week.
    /// </summary>
    /// <param name="guildId">The persistent guild id.</param>
    /// <param name="guildName">The guild name.</param>
    /// <param name="nowUtc">The time.</param>
    public void SetCastleSiegeWinner(Guid guildId, string? guildName, DateTime nowUtc)
    {
        lock (this.Store.Lock)
        {
            this.RollWeek(nowUtc);
            this.Store.Data.CastleSiegeGuildId = guildId;
            this.Store.Data.CastleSiegeGuildName = guildName;
            this.Store.Changed();
        }
    }

    /// <summary>
    /// Gets the ranking of the current week.
    /// </summary>
    /// <param name="category">The ranking.</param>
    /// <param name="nowUtc">The time.</param>
    /// <param name="count">The most lines.</param>
    /// <returns>The lines: rank, character id and entry.</returns>
    public List<(int Rank, string CharacterId, WeeklyRankingEntry Entry)> Ranking(WeeklyCategory category, DateTime nowUtc, int count = PaidRanks)
    {
        lock (this.Store.Lock)
        {
            this.RollWeek(nowUtc);
            return RankingLocked(this.Store.Data, category, count);
        }
    }

    /// <summary>
    /// Gets the points of a character in the current week.
    /// </summary>
    /// <param name="category">The ranking.</param>
    /// <param name="characterId">The character id.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The points and the rank (0 without points).</returns>
    public (long Points, int Rank) PointsOf(WeeklyCategory category, string characterId, DateTime nowUtc)
    {
        lock (this.Store.Lock)
        {
            this.RollWeek(nowUtc);
            var all = RankingLocked(this.Store.Data, category, int.MaxValue);
            var index = all.FindIndex(r => r.CharacterId == characterId);
            return index < 0 ? (0, 0) : (all[index].Entry.Points, all[index].Rank);
        }
    }

    /// <summary>
    /// Starts a new week when the week changed: the finished week's top 10 (and castle siege winner) become a
    /// <see cref="WeeklyResult"/> which waits to be paid. The caller holds the lock or calls through a public method.
    /// </summary>
    /// <param name="nowUtc">The time.</param>
    public void RollWeek(DateTime nowUtc)
    {
        lock (this.Store.Lock)
        {
            var data = this.Store.Data;
            var current = this.Clock.WeekKey(nowUtc);
            if (data.WeekKey == current)
            {
                return;
            }

            if (data.WeekKey.Length > 0 && !data.PaidWeeks.Contains(data.WeekKey))
            {
                var result = new WeeklyResult { Week = data.WeekKey, CastleSiegeGuild = data.CastleSiegeGuildName, CastleSiegeGuildId = data.CastleSiegeGuildId };
                foreach (var category in new[] { WeeklyCategory.BossHunt, WeeklyCategory.BloodCastle, WeeklyCategory.DevilSquare, WeeklyCategory.ChaosCastle })
                {
                    var ranking = this.Configuration.Ranking(category)!;
                    result.Winners[category] = RankingLocked(data, category, PaidRanks)
                        .Select(r => new WeeklyWinner
                        {
                            Rank = r.Rank,
                            CharacterId = r.CharacterId,
                            Name = r.Entry.Name,
                            Login = r.Entry.Login,
                            Points = r.Entry.Points,
                            Coins = ranking.Enabled ? ranking.CoinsOf(r.Rank) : 0,
                            CoinType = ranking.CoinType,
                        })
                        .ToList();
                }

                data.PendingWeeks.RemoveAll(w => w.Week == result.Week);
                data.PendingWeeks.Add(result);
                data.LastWeek = result;
            }

            data.WeekKey = current;
            data.Week.Clear();
            data.CastleSiegeGuildId = null;
            data.CastleSiegeGuildName = null;
            this.Store.Changed();
        }
    }

    /// <summary>
    /// Takes the finished weeks which weren't paid yet; each is paid once (<see cref="MarkPaid"/> after paying).
    /// </summary>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The weeks.</returns>
    public List<WeeklyResult> TakeUnpaidWeeks(DateTime nowUtc)
    {
        lock (this.Store.Lock)
        {
            this.RollWeek(nowUtc);
            return this.Store.Data.PendingWeeks.Where(w => !this.Store.Data.PaidWeeks.Contains(w.Week)).ToList();
        }
    }

    /// <summary>
    /// Marks a week as paid.
    /// </summary>
    /// <param name="result">The week, with the castle siege members who were paid.</param>
    public void MarkPaid(WeeklyResult result)
    {
        var week = result.Week;
        lock (this.Store.Lock)
        {
            var data = this.Store.Data;
            if (data.LastWeek is null || string.CompareOrdinal(data.LastWeek.Week, week) <= 0)
            {
                data.LastWeek = result;
            }

            if (!data.PaidWeeks.Contains(week))
            {
                data.PaidWeeks.Add(week);
                if (data.PaidWeeks.Count > 60)
                {
                    data.PaidWeeks.RemoveAt(0);
                }
            }

            data.PendingWeeks.RemoveAll(w => w.Week == week);
            this.Store.Changed();
        }
    }

    private static string TierCaption(string tier, int rank) => tier switch
    {
        "participation" => "participation",
        "lasthit" => "last hit",
        _ => string.Create(CultureInfo.InvariantCulture, $"rank {rank}"),
    };

    private static MissionReward CopyReward(MissionReward reward) => new()
    {
        Zen = reward.Zen,
        Coins = reward.Coins,
        CoinType = reward.CoinType,
        Items = reward.Items.Where(i => i.Count > 0).Select(i => new MissionItemReward { Group = i.Group, Number = i.Number, Level = i.Level, Count = i.Count }).ToList(),
    };

    private static WorldBossAliveState Copy(WorldBossAliveState alive) => new()
    {
        BossId = alive.BossId,
        ServerId = alive.ServerId,
        InstanceId = alive.InstanceId,
        MapNumber = alive.MapNumber,
        X = alive.X,
        Y = alive.Y,
        SpawnedAtUtc = alive.SpawnedAtUtc,
        DespawnAtUtc = alive.DespawnAtUtc,
        MaximumHealth = alive.MaximumHealth,
        Damage = alive.Damage.ToDictionary(d => d.Key, d => new WorldBossDamageEntry { CharacterId = d.Value.CharacterId, Name = d.Value.Name, Login = d.Value.Login, Party = d.Value.Party, Damage = d.Value.Damage }, StringComparer.Ordinal),
    };

    private static WorldBossKillRecord NewRecord(WorldBossAliveState alive, WorldBossDefinition boss, DateTime nowUtc, bool killed, long total) => new()
    {
        InstanceId = alive.InstanceId,
        BossId = alive.BossId,
        NameEn = boss.NameEn,
        NameVi = boss.NameVi,
        MapNumber = alive.MapNumber,
        ServerId = alive.ServerId,
        SpawnedAtUtc = alive.SpawnedAtUtc,
        EndedAtUtc = nowUtc,
        Killed = killed,
        TotalDamage = total,
    };

    private static List<(int Rank, string CharacterId, WeeklyRankingEntry Entry)> RankingLocked(WorldBossStoreData data, WeeklyCategory category, int count)
    {
        if (!data.Week.TryGetValue(category, out var entries))
        {
            return new List<(int, string, WeeklyRankingEntry)>();
        }

        return entries
            .Where(e => e.Value.Points > 0)
            .OrderByDescending(e => e.Value.Points)
            .ThenBy(e => e.Value.UpdatedAtUtc)
            .ThenBy(e => e.Value.Name, StringComparer.Ordinal)
            .Take(count)
            .Select((e, index) => (index + 1, e.Key, e.Value))
            .ToList();
    }

    private static string ScheduleKey(WorldBossDefinition boss) =>
        string.Join('|', boss.ScheduleKind, boss.EveryHours, boss.StartTime, boss.FixedTimes, string.Join(',', (boss.Days ?? []).OrderBy(d => d)));

    private void AddPointsLocked(WeeklyCategory category, string characterId, string name, string login, long points, DateTime nowUtc)
    {
        var data = this.Store.Data;
        if (!data.Week.TryGetValue(category, out var entries))
        {
            entries = new Dictionary<string, WeeklyRankingEntry>(StringComparer.Ordinal);
            data.Week[category] = entries;
        }

        if (!entries.TryGetValue(characterId, out var entry))
        {
            entry = new WeeklyRankingEntry();
            entries[characterId] = entry;
        }

        entry.Name = name;
        entry.Login = login;
        entry.Points += points;
        entry.UpdatedAtUtc = nowUtc;
    }

    private void Keep(WorldBossKillRecord record)
    {
        var kills = this.Store.Data.Kills;
        kills.Insert(0, record);
        if (kills.Count > KeptKills)
        {
            kills.RemoveRange(KeptKills, kills.Count - KeptKills);
        }
    }

    private WorldBossState StateOf(WorldBossDefinition boss, DateTime nowUtc)
    {
        var data = this.Store.Data;
        if (!data.Bosses.TryGetValue(boss.Id, out var state))
        {
            state = new WorldBossState();
            data.Bosses[boss.Id] = state;
        }

        var key = ScheduleKey(boss);
        if (state.ScheduleKey != key || state.NextSpawnUtc is null)
        {
            state.ScheduleKey = key;
            state.NextSpawnUtc = WorldBossSchedule.Next(boss, this.TimeZone, nowUtc);
            this.Store.Changed();
        }

        return state;
    }
}
