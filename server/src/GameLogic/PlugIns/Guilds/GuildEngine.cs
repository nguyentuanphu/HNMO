// <copyright file="GuildEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Guilds;

using System.Globalization;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// The rules of HNMO's guild levels (pure, unit-tested): guild experience from the members' play with a daily cap per
/// member, levels by experience, the buff of a level, the weekly ladder (Monday 00:00 in the configured time zone) whose
/// finished weeks are paid once, the castle siege results and the rewards which wait for room in the bag.
/// </summary>
public sealed class GuildEngine
{
    /// <summary>The ranks a finished week keeps.</summary>
    public const int KeptRanks = 10;

    /// <summary>The castle sieges kept.</summary>
    public const int KeptSieges = 20;

    /// <summary>
    /// Initializes a new instance of the <see cref="GuildEngine"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="store">The store.</param>
    public GuildEngine(GuildsConfiguration configuration, GuildStore store)
    {
        this.Configuration = configuration;
        this.Store = store;
        this.Clock = new MissionClock(configuration.TimeZone, 0);
    }

    /// <summary>Gets the configuration.</summary>
    public GuildsConfiguration Configuration { get; }

    /// <summary>Gets the store.</summary>
    public GuildStore Store { get; }

    /// <summary>Gets the clock of the days and weeks.</summary>
    public MissionClock Clock { get; }

    /// <summary>Gets the levels, lowest first.</summary>
    public IReadOnlyList<GuildLevelDefinition> Levels => this.Configuration.Levels.Where(l => l.Level >= 1).OrderBy(l => l.Level).ToList();

    /// <summary>
    /// The coin grant of a ladder reward for a member: the coins, plus the master's bonus, with a unique reference.
    /// </summary>
    /// <param name="week">The week.</param>
    /// <param name="line">The guild's line.</param>
    /// <param name="reward">The reward.</param>
    /// <param name="memberId">The member's character id.</param>
    /// <param name="memberName">The member's name.</param>
    /// <param name="login">The member's login name.</param>
    /// <param name="isMaster">Whether the member is the guild master.</param>
    /// <returns>The grant; <c>null</c> without coins.</returns>
    public static CoinGrant? LadderGrant(string week, GuildLadderLine line, GuildLadderReward reward, Guid memberId, string memberName, string login, bool isMaster)
    {
        var coins = reward.CoinsPerMember + (isMaster ? reward.MasterBonusCoins : 0);
        if (coins <= 0)
        {
            return null;
        }

        return new CoinGrant(
            login,
            reward.CoinType,
            coins,
            string.Create(CultureInfo.InvariantCulture, $"Guild ladder, week of {week}: rank {line.Rank} ({line.Name}, {memberName})"),
            LadderReference(week, line.GuildId, memberId));
    }

    /// <summary>Gets the reference of a member's ladder reward.</summary>
    /// <param name="week">The week.</param>
    /// <param name="guildId">The guild.</param>
    /// <param name="memberId">The member.</param>
    /// <returns>The reference.</returns>
    public static string LadderReference(string week, Guid guildId, Guid memberId) => $"hnmo-guild-week:{week}:{guildId:N}:{memberId:N}";

    /// <summary>
    /// Gets the level of an experience: the highest level whose experience is reached (at least 1).
    /// </summary>
    /// <param name="xp">The experience.</param>
    /// <returns>The level.</returns>
    public int LevelOf(long xp)
    {
        var level = 1;
        foreach (var definition in this.Levels)
        {
            if (xp >= definition.Xp)
            {
                level = Math.Max(level, definition.Level);
            }
        }

        return level;
    }

    /// <summary>Gets the definition of a level.</summary>
    /// <param name="level">The level.</param>
    /// <returns>The definition, if it's configured.</returns>
    public GuildLevelDefinition? Level(int level) => this.Levels.FirstOrDefault(l => l.Level == level);

    /// <summary>Gets the next level after one, if any.</summary>
    /// <param name="level">The level.</param>
    /// <returns>The next level.</returns>
    public GuildLevelDefinition? NextLevel(int level) => this.Levels.FirstOrDefault(l => l.Level > level);

    /// <summary>
    /// Gets the buff of a level: its own bonuses, when the buffs are on and it gives any.
    /// </summary>
    /// <param name="level">The level.</param>
    /// <returns>The buff; <c>null</c> for none.</returns>
    public GuildLevelDefinition? BuffOf(int level) =>
        this.Configuration.BuffsEnabled && this.Level(level) is { HasBuff: true } definition ? definition : null;

    /// <summary>
    /// Gets the experience of a source for a number of times (percent for the world boss damage); 0 when it's off.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="times">The times.</param>
    /// <returns>The experience.</returns>
    public int XpFor(GuildXpSource source, double times = 1)
    {
        var setting = this.Configuration.Source(source);
        if (!setting.Enabled || setting.Xp <= 0 || times <= 0)
        {
            return 0;
        }

        return (int)Math.Min(int.MaxValue, Math.Floor(setting.Xp * times));
    }

    /// <summary>
    /// Adds guild experience a member earned; the member's daily cap applies (not to the castle siege).
    /// </summary>
    /// <param name="guildId">The persistent guild id.</param>
    /// <param name="guildName">The guild's name.</param>
    /// <param name="characterId">The member's character id; <c>null</c> for the guild itself (no cap).</param>
    /// <param name="memberName">The member's name (for the week's contributions).</param>
    /// <param name="source">The source.</param>
    /// <param name="amount">The experience.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The result.</returns>
    public GuildXpResult AddXp(Guid guildId, string? guildName, string? characterId, string? memberName, GuildXpSource source, int amount, DateTime nowUtc)
    {
        lock (this.Store.Lock)
        {
            this.Roll(nowUtc);
            var data = this.Store.Data;
            var guild = this.GuildLocked(guildId, guildName);
            var oldLevel = this.LevelOf(guild.Xp);
            if (amount <= 0)
            {
                return new GuildXpResult(0, oldLevel, oldLevel);
            }

            var cap = this.Configuration.DailyCapPerMember;
            if (source != GuildXpSource.CastleSiege && characterId is not null && cap > 0)
            {
                data.Today.TryGetValue(characterId, out var today);
                amount = Math.Min(amount, Math.Max(0, cap - today));
                if (amount <= 0)
                {
                    return new GuildXpResult(0, oldLevel, oldLevel);
                }

                data.Today[characterId] = today + amount;
            }

            guild.Xp += amount;
            guild.WeekXp += amount;
            guild.WeekUpdatedAtUtc = nowUtc;
            if (!string.IsNullOrEmpty(memberName))
            {
                guild.WeekMembers[memberName] = guild.WeekMembers.GetValueOrDefault(memberName) + amount;
            }

            guild.Level = this.LevelOf(guild.Xp);
            if (guild.Level > oldLevel)
            {
                guild.LevelUpAtUtc = nowUtc;
            }

            this.Store.Changed();
            return new GuildXpResult(amount, oldLevel, guild.Level);
        }
    }

    /// <summary>
    /// Gets the capped guild experience a member earned today.
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The experience.</returns>
    public int Today(string characterId, DateTime nowUtc)
    {
        lock (this.Store.Lock)
        {
            this.Roll(nowUtc);
            return this.Store.Data.Today.GetValueOrDefault(characterId);
        }
    }

    /// <summary>
    /// Gets the progress of a guild.
    /// </summary>
    /// <param name="guildId">The persistent guild id.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>A copy of the progress; <c>null</c> when it has none yet.</returns>
    public GuildProgress? Progress(Guid guildId, DateTime nowUtc)
    {
        lock (this.Store.Lock)
        {
            this.Roll(nowUtc);
            if (!this.Store.Data.Guilds.TryGetValue(guildId, out var guild))
            {
                return null;
            }

            // A level lowered in the configuration counts from the experience; a level is never lost otherwise.
            var level = this.LevelOf(guild.Xp);
            return new GuildProgress
            {
                Name = guild.Name, Xp = guild.Xp, Level = level, WeekXp = guild.WeekXp, WeekUpdatedAtUtc = guild.WeekUpdatedAtUtc,
                WeekMembers = new Dictionary<string, long>(guild.WeekMembers, StringComparer.Ordinal), LevelUpAtUtc = guild.LevelUpAtUtc,
            };
        }
    }

    /// <summary>
    /// Gets all guilds with their progress, by experience.
    /// </summary>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The guilds.</returns>
    public List<(Guid GuildId, GuildProgress Progress)> AllGuilds(DateTime nowUtc)
    {
        lock (this.Store.Lock)
        {
            this.Roll(nowUtc);
            return this.Store.Data.Guilds.Select(g => (g.Key, g.Value)).OrderByDescending(g => g.Value.Xp).ThenBy(g => g.Value.Name, StringComparer.Ordinal).ToList();
        }
    }

    /// <summary>
    /// Gets this week's ladder: the guilds with points, best first; a tie goes to the guild which got there first.
    /// </summary>
    /// <param name="nowUtc">The time.</param>
    /// <param name="count">The lines.</param>
    /// <returns>The ladder.</returns>
    public List<GuildLadderEntry> Ladder(DateTime nowUtc, int count = KeptRanks)
    {
        lock (this.Store.Lock)
        {
            this.Roll(nowUtc);
            return LadderLocked(this.Store.Data, count);
        }
    }

    /// <summary>
    /// Gets the rank of a guild in this week's ladder.
    /// </summary>
    /// <param name="guildId">The guild.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The rank; 0 without points.</returns>
    public int RankOf(Guid guildId, DateTime nowUtc) =>
        this.Ladder(nowUtc, int.MaxValue).FirstOrDefault(e => e.GuildId == guildId)?.Rank ?? 0;

    /// <summary>
    /// Gets the reward of a ladder rank, for a guild with enough points.
    /// </summary>
    /// <param name="rank">The rank.</param>
    /// <param name="points">The guild's points.</param>
    /// <returns>The reward, if any.</returns>
    public GuildLadderReward? RewardOf(int rank, long points)
    {
        if (!this.Configuration.LadderEnabled || points < Math.Max(1, this.Configuration.LadderMinimumXp))
        {
            return null;
        }

        return this.Configuration.LadderRewards.FirstOrDefault(r => rank >= r.RankFrom && rank <= Math.Max(r.RankFrom, r.RankTo));
    }

    /// <summary>
    /// Keeps a castle siege won by a guild: its experience, the ladder bonus and the result.
    /// </summary>
    /// <param name="guildId">The persistent guild id.</param>
    /// <param name="guildName">The guild's name.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The experience result.</returns>
    public GuildXpResult RecordCastleSiege(Guid guildId, string? guildName, DateTime nowUtc)
    {
        var result = this.AddXp(guildId, guildName, null, null, GuildXpSource.CastleSiege, this.XpFor(GuildXpSource.CastleSiege), nowUtc);
        lock (this.Store.Lock)
        {
            var data = this.Store.Data;
            var guild = this.GuildLocked(guildId, guildName);
            if (this.Configuration.CastleSiegeLadderBonus > 0)
            {
                guild.WeekXp += this.Configuration.CastleSiegeLadderBonus;
                guild.WeekUpdatedAtUtc = nowUtc;
            }

            data.Sieges.Insert(0, new GuildSiegeResult { AtUtc = nowUtc, GuildId = guildId, Name = guild.Name });
            if (data.Sieges.Count > KeptSieges)
            {
                data.Sieges.RemoveRange(KeptSieges, data.Sieges.Count - KeptSieges);
            }

            this.Store.Changed();
        }

        return result;
    }

    /// <summary>Gets the castle sieges, newest first.</summary>
    /// <returns>The sieges.</returns>
    public List<GuildSiegeResult> Sieges()
    {
        lock (this.Store.Lock)
        {
            return this.Store.Data.Sieges.ToList();
        }
    }

    /// <summary>Gets the last finished week.</summary>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The week, if any.</returns>
    public GuildWeekResult? LastWeek(DateTime nowUtc)
    {
        lock (this.Store.Lock)
        {
            this.Roll(nowUtc);
            return this.Store.Data.LastWeek;
        }
    }

    /// <summary>
    /// Takes the finished weeks which weren't paid yet; each is paid once (<see cref="MarkPaid"/> after paying).
    /// </summary>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The weeks.</returns>
    public List<GuildWeekResult> TakeUnpaidWeeks(DateTime nowUtc)
    {
        lock (this.Store.Lock)
        {
            this.Roll(nowUtc);
            return this.Store.Data.PendingWeeks.Where(w => !this.Store.Data.PaidWeeks.Contains(w.Week)).ToList();
        }
    }

    /// <summary>
    /// Notes a reference of a week as paid (the coins of a member); a retry skips it.
    /// </summary>
    /// <param name="week">The week.</param>
    /// <param name="reference">The reference.</param>
    /// <returns><c>true</c>, if it wasn't paid before.</returns>
    public bool NotePaid(GuildWeekResult week, string reference)
    {
        lock (this.Store.Lock)
        {
            if (week.PaidReferences.Contains(reference))
            {
                return false;
            }

            week.PaidReferences.Add(reference);
            this.Store.Changed();
            return true;
        }
    }

    /// <summary>
    /// Marks a week as paid.
    /// </summary>
    /// <param name="week">The week.</param>
    public void MarkPaid(GuildWeekResult week)
    {
        lock (this.Store.Lock)
        {
            var data = this.Store.Data;
            if (!data.PaidWeeks.Contains(week.Week))
            {
                data.PaidWeeks.Add(week.Week);
                if (data.PaidWeeks.Count > 60)
                {
                    data.PaidWeeks.RemoveAt(0);
                }
            }

            data.PendingWeeks.RemoveAll(w => w.Week == week.Week);
            if (data.LastWeek is null || string.CompareOrdinal(data.LastWeek.Week, week.Week) <= 0)
            {
                data.LastWeek = week;
            }

            this.Store.Changed();
        }
    }

    /// <summary>
    /// Adds a reward which waits for room in the bag; one with the same reference isn't added twice.
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <param name="reward">The reward.</param>
    /// <returns><c>true</c>, if it was added.</returns>
    public bool AddOwed(string characterId, GuildOwedReward reward)
    {
        lock (this.Store.Lock)
        {
            var data = this.Store.Data;
            if (!data.Owed.TryGetValue(characterId, out var list))
            {
                list = new List<GuildOwedReward>();
                data.Owed[characterId] = list;
            }

            if (list.Any(r => r.Reference == reward.Reference))
            {
                return false;
            }

            list.Add(reward);
            this.Store.Changed();
            return true;
        }
    }

    /// <summary>Gets the waiting rewards of a character.</summary>
    /// <param name="characterId">The character id.</param>
    /// <returns>The rewards.</returns>
    public List<GuildOwedReward> Owed(string characterId)
    {
        lock (this.Store.Lock)
        {
            return this.Store.Data.Owed.TryGetValue(characterId, out var list) ? list.ToList() : new List<GuildOwedReward>();
        }
    }

    /// <summary>Takes the waiting rewards of a character (give back what wasn't given with <see cref="ReturnOwed"/>).</summary>
    /// <param name="characterId">The character id.</param>
    /// <returns>The rewards.</returns>
    public List<GuildOwedReward> TakeOwed(string characterId)
    {
        lock (this.Store.Lock)
        {
            if (!this.Store.Data.Owed.Remove(characterId, out var list))
            {
                return new List<GuildOwedReward>();
            }

            this.Store.Changed();
            return list;
        }
    }

    /// <summary>Puts rewards back which weren't given.</summary>
    /// <param name="characterId">The character id.</param>
    /// <param name="rewards">The rewards.</param>
    public void ReturnOwed(string characterId, IEnumerable<GuildOwedReward> rewards)
    {
        foreach (var reward in rewards)
        {
            this.AddOwed(characterId, reward);
        }
    }

    /// <summary>
    /// Starts a new day or week when it changed: a finished week's top 10 becomes a <see cref="GuildWeekResult"/>
    /// which waits to be paid, and every guild's week points start from 0.
    /// </summary>
    /// <param name="nowUtc">The time.</param>
    public void Roll(DateTime nowUtc)
    {
        lock (this.Store.Lock)
        {
            var data = this.Store.Data;
            var day = this.Clock.DayKey(nowUtc);
            if (data.DayKey != day)
            {
                data.DayKey = day;
                data.Today.Clear();
                this.Store.Changed();
            }

            var week = this.Clock.WeekKey(nowUtc);
            if (data.WeekKey == week)
            {
                return;
            }

            if (data.WeekKey.Length > 0 && !data.PaidWeeks.Contains(data.WeekKey) && data.PendingWeeks.All(w => w.Week != data.WeekKey))
            {
                var result = new GuildWeekResult { Week = data.WeekKey };
                foreach (var entry in LadderLocked(data, KeptRanks))
                {
                    var reward = this.RewardOf(entry.Rank, entry.Progress.WeekXp);
                    result.Lines.Add(new GuildLadderLine
                    {
                        Rank = entry.Rank, GuildId = entry.GuildId, Name = entry.Progress.Name, Xp = entry.Progress.WeekXp,
                        CoinsPerMember = reward?.CoinsPerMember ?? 0, CoinType = reward?.CoinType ?? default,
                    });
                }

                data.PendingWeeks.Add(result);
                data.LastWeek = result;
            }

            data.WeekKey = week;
            foreach (var guild in data.Guilds.Values)
            {
                guild.WeekXp = 0;
                guild.WeekMembers.Clear();
            }

            this.Store.Changed();
        }
    }

    private static List<GuildLadderEntry> LadderLocked(GuildStoreData data, int count) =>
        data.Guilds.Where(g => g.Value.WeekXp > 0)
            .OrderByDescending(g => g.Value.WeekXp)
            .ThenBy(g => g.Value.WeekUpdatedAtUtc)
            .ThenBy(g => g.Key)
            .Take(count)
            .Select((g, index) => new GuildLadderEntry(index + 1, g.Key, g.Value))
            .ToList();

    private GuildProgress GuildLocked(Guid guildId, string? guildName)
    {
        var data = this.Store.Data;
        if (!data.Guilds.TryGetValue(guildId, out var guild))
        {
            guild = new GuildProgress { Name = guildName ?? string.Empty };
            data.Guilds[guildId] = guild;
            this.Store.Changed();
        }
        else if (!string.IsNullOrEmpty(guildName) && guild.Name != guildName)
        {
            guild.Name = guildName;
            this.Store.Changed();
        }

        return guild;
    }
}
