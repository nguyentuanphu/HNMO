// <copyright file="TopRaceEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TopRaces;

using System.Globalization;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// The rules of HNMO's top races (pure, unit-tested): the periods (days, weeks from Monday, months, custom wall-clock
/// dates, all in the configured time zone with its daylight saving), the values counted while a race runs, the tables
/// (all classes, one class or one per class) with ties going to whoever got there first, the finished races with their
/// prizes, paid once, and the prizes which wait for room in the bag.
/// </summary>
public sealed class TopRaceEngine
{
    /// <summary>The ranks a finished table keeps at least.</summary>
    public const int KeptRanks = 10;

    /// <summary>The format of the custom dates.</summary>
    public const string DateFormat = "yyyy-MM-dd HH:mm";

    /// <summary>
    /// Initializes a new instance of the <see cref="TopRaceEngine"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="store">The store.</param>
    public TopRaceEngine(TopRacesConfiguration configuration, TopRaceStore store)
    {
        this.Configuration = configuration;
        this.Store = store;
        this.Clock = new MissionClock(configuration.TimeZone, 0);
    }

    /// <summary>Gets the configuration.</summary>
    public TopRacesConfiguration Configuration { get; }

    /// <summary>Gets the store.</summary>
    public TopRaceStore Store { get; }

    /// <summary>Gets the clock of the days, weeks and months.</summary>
    public MissionClock Clock { get; }

    /// <summary>Gets the enabled races with a valid id.</summary>
    public IReadOnlyList<TopRaceDefinition> Races =>
        this.Configuration.Races.Where(r => r.Enabled && !string.IsNullOrWhiteSpace(r.Id)).GroupBy(r => r.Id, StringComparer.Ordinal).Select(g => g.First()).ToList();

    /// <summary>Gets the class of a character class number (each class with its evolutions).</summary>
    /// <param name="characterClassNumber">The character class number.</param>
    /// <returns>The class.</returns>
    public static int ClassOf(int characterClassNumber) => Math.Clamp(characterClassNumber / 4, 0, 6);

    /// <summary>Gets the key of a guild in a guild race.</summary>
    /// <param name="guildId">The persistent guild id.</param>
    /// <returns>The key.</returns>
    public static string GuildKey(Guid guildId) => $"guild:{guildId:N}";

    /// <summary>Gets the guild id of a guild key.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The guild id, if it's a guild key.</returns>
    public static Guid? GuildIdOf(string key) =>
        key.StartsWith("guild:", StringComparison.Ordinal) && Guid.TryParseExact(key[6..], "N", out var id) ? id : null;

    /// <summary>Gets the key of a running race.</summary>
    /// <param name="raceId">The race id.</param>
    /// <param name="periodKey">The period key.</param>
    /// <returns>The key.</returns>
    public static string RunKey(string raceId, string periodKey) => raceId + "|" + periodKey;

    /// <summary>
    /// Converts a wall-clock time of a time zone to UTC: a time which doesn't exist (the hour skipped when daylight
    /// saving starts) moves to the first valid minute after it; a time which exists twice (when it ends) is the first.
    /// </summary>
    /// <param name="local">The wall-clock time.</param>
    /// <param name="zone">The time zone.</param>
    /// <returns>The moment.</returns>
    public static DateTime LocalToUtc(DateTime local, TimeZoneInfo zone)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        for (var i = 0; i < 24 * 60 && zone.IsInvalidTime(local); i++)
        {
            local = local.AddMinutes(1);
        }

        if (zone.IsAmbiguousTime(local))
        {
            var offset = zone.GetAmbiguousTimeOffsets(local).Max();
            return DateTime.SpecifyKind(local - offset, DateTimeKind.Utc);
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }

    /// <summary>Parses a custom date ("yyyy-MM-dd HH:mm", also with a "T").</summary>
    /// <param name="text">The text.</param>
    /// <returns>The wall-clock time, if it's valid.</returns>
    public static DateTime? ParseLocal(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var normalized = text.Trim().Replace('T', ' ');
        string[] formats = [DateFormat, "yyyy-MM-dd H:mm", "yyyy-MM-dd"];
        return DateTime.TryParseExact(normalized, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local) ? local : null;
    }

    /// <summary>
    /// Gets the classes which have a table in a race: -1 (all) unless it's per class; one class for a one-class race.
    /// </summary>
    /// <param name="race">The race.</param>
    /// <returns>The classes.</returns>
    public static IReadOnlyList<int> TableClasses(TopRaceDefinition race) =>
        race.IsGuildRace || race.Scope == TopRaceScope.AllClasses ? [-1]
        : race.Scope == TopRaceScope.OneClass ? [(int)race.Class]
        : Enum.GetValues<TopRaceClass>().Select(c => (int)c).ToList();

    /// <summary>Gets the table of a participant in a race.</summary>
    /// <param name="race">The race.</param>
    /// <param name="participantClass">The participant's class.</param>
    /// <returns>The table's class.</returns>
    public static int TableOf(TopRaceDefinition race, int participantClass) =>
        race.IsGuildRace || race.Scope == TopRaceScope.AllClasses ? -1 : race.Scope == TopRaceScope.OneClass ? (int)race.Class : participantClass;

    /// <summary>
    /// Gets the prize of a rank, for a value which reaches the race's minimum.
    /// </summary>
    /// <param name="race">The race.</param>
    /// <param name="rank">The rank.</param>
    /// <param name="value">The value.</param>
    /// <returns>The prize, if any.</returns>
    public static TopRacePrize? PrizeOf(TopRaceDefinition race, int rank, long value)
    {
        if (rank < 1 || value < Math.Max(1, race.Minimum))
        {
            return null;
        }

        return race.Prizes.FirstOrDefault(p => rank >= p.RankFrom && rank <= Math.Max(p.RankFrom, p.RankTo) && !p.IsEmpty);
    }

    /// <summary>Gets the reference of a prize.</summary>
    /// <param name="result">The result.</param>
    /// <param name="table">The table's class.</param>
    /// <param name="key">The line's key.</param>
    /// <param name="memberId">The guild member, for a guild race.</param>
    /// <returns>The reference.</returns>
    public static string PrizeReference(TopRaceResult result, int table, string key, Guid? memberId = null) =>
        string.Create(CultureInfo.InvariantCulture, $"hnmo-top-race:{result.RaceId}:{result.PeriodKey}:{table}:{key}") + (memberId is { } m ? $":{m:N}" : string.Empty);

    /// <summary>
    /// The coin grant of a prize, with a unique reference.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <param name="table">The table's class.</param>
    /// <param name="line">The line.</param>
    /// <param name="login">The account which gets it.</param>
    /// <param name="memberId">The guild member, for a guild race.</param>
    /// <param name="memberName">The guild member's name, for a guild race.</param>
    /// <returns>The grant; <c>null</c> without coins.</returns>
    public static CoinGrant? PrizeGrant(TopRaceResult result, int table, TopRaceLine line, string login, Guid? memberId = null, string? memberName = null)
    {
        if (line.Coins <= 0 || string.IsNullOrEmpty(login))
        {
            return null;
        }

        var who = memberName is null ? line.Name : $"{line.Name}, {memberName}";
        return new CoinGrant(
            login,
            line.CoinType,
            line.Coins,
            string.Create(CultureInfo.InvariantCulture, $"Top race \"{result.TitleEn}\" ({result.PeriodKey}): rank {line.Rank} ({who})"),
            PrizeReference(result, table, line.Key, memberId));
    }

    /// <summary>
    /// Fills placeholders in braces, e.g. {race}.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="values">The values.</param>
    /// <returns>The filled text.</returns>
    public static string Fill(string? text, IReadOnlyDictionary<string, string> values)
    {
        var result = text ?? string.Empty;
        foreach (var (key, value) in values)
        {
            result = result.Replace("{" + key + "}", value, StringComparison.Ordinal);
        }

        return result;
    }

    /// <summary>
    /// Gets the period of a race which contains a moment.
    /// </summary>
    /// <param name="race">The race.</param>
    /// <param name="nowUtc">The moment.</param>
    /// <returns>The period; <c>null</c> when a custom race doesn't run then (or its dates are invalid).</returns>
    public TopRaceWindow? WindowOf(TopRaceDefinition race, DateTime nowUtc)
    {
        switch (race.Period)
        {
            case TopRacePeriod.Daily:
            {
                var day = this.Clock.Day(nowUtc);
                return new TopRaceWindow("d" + MissionClock.Key(day), this.Clock.StartOf(day), this.Clock.StartOf(day.AddDays(1)));
            }

            case TopRacePeriod.Weekly:
            {
                var monday = this.Clock.WeekStart(nowUtc);
                return new TopRaceWindow("w" + MissionClock.Key(monday), this.Clock.StartOf(monday), this.Clock.StartOf(monday.AddDays(7)));
            }

            case TopRacePeriod.Monthly:
            {
                var day = this.Clock.Day(nowUtc);
                var first = new DateOnly(day.Year, day.Month, 1);
                return new TopRaceWindow("m" + first.ToString("yyyy-MM", CultureInfo.InvariantCulture), this.Clock.StartOf(first), this.Clock.StartOf(first.AddMonths(1)));
            }

            default:
            {
                var window = this.CustomWindow(race);
                return window is not null && window.StartUtc <= nowUtc && nowUtc < window.EndUtc ? window : null;
            }
        }
    }

    /// <summary>
    /// Gets the period of a custom race, whether it runs or not.
    /// </summary>
    /// <param name="race">The race.</param>
    /// <returns>The period; <c>null</c> for other races or invalid dates.</returns>
    public TopRaceWindow? CustomWindow(TopRaceDefinition race)
    {
        if (race.Period != TopRacePeriod.Custom || ParseLocal(race.Start) is not { } start || ParseLocal(race.End) is not { } end)
        {
            return null;
        }

        var startUtc = LocalToUtc(start, this.Clock.TimeZone);
        var endUtc = LocalToUtc(end, this.Clock.TimeZone);
        return endUtc > startUtc ? new TopRaceWindow("c" + start.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture), startUtc, endUtc) : null;
    }

    /// <summary>Gets an enabled race by its id.</summary>
    /// <param name="raceId">The id.</param>
    /// <returns>The race.</returns>
    public TopRaceDefinition? Race(string raceId) => this.Races.FirstOrDefault(r => r.Id == raceId);

    /// <summary>
    /// Starts the races whose period began and finishes those whose period ended: a finished race's tables (with their
    /// prizes) go into the history and wait to be paid. A race which was turned off or removed while it ran ends without
    /// a result.
    /// </summary>
    /// <param name="nowUtc">The time.</param>
    public void Roll(DateTime nowUtc)
    {
        lock (this.Store.Lock)
        {
            var data = this.Store.Data;
            foreach (var (key, run) in data.Runs.ToList())
            {
                var race = this.Race(run.RaceId);
                if (race is null)
                {
                    data.Runs.Remove(key);
                    this.Store.Changed();
                    continue;
                }

                if (run.EndUtc > nowUtc)
                {
                    // A changed period (e.g. weekly to daily, or new custom dates) ends the old run early.
                    var current = this.WindowOf(race, nowUtc) ?? (race.Period == TopRacePeriod.Custom ? this.CustomWindow(race) : null);
                    if (current is not null && current.Key == run.PeriodKey)
                    {
                        if (current.EndUtc != run.EndUtc)
                        {
                            // A custom race's new end.
                            run.EndUtc = current.EndUtc;
                            this.Store.Changed();
                        }

                        if (run.EndUtc > nowUtc)
                        {
                            continue;
                        }
                    }
                }

                data.Runs.Remove(key);
                if (data.History.All(h => !(h.RaceId == run.RaceId && h.PeriodKey == run.PeriodKey)))
                {
                    data.History.Insert(0, this.Finish(race, run));
                }

                this.Store.Changed();
            }

            this.TrimHistoryLocked();
            foreach (var race in this.Races)
            {
                if (this.WindowOf(race, nowUtc) is not { } window)
                {
                    continue;
                }

                var key = RunKey(race.Id, window.Key);
                if (data.Runs.ContainsKey(key) || data.History.Any(h => h.RaceId == race.Id && h.PeriodKey == window.Key))
                {
                    continue;
                }

                data.Runs[key] = new TopRaceRun { RaceId = race.Id, PeriodKey = window.Key, StartUtc = window.StartUtc, EndUtc = window.EndUtc };
                this.Store.Changed();
            }
        }
    }

    /// <summary>
    /// Adds to the value of a participant in every running race of a kind.
    /// </summary>
    /// <param name="participant">The participant.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="amount">The amount.</param>
    /// <param name="nowUtc">The time.</param>
    /// <param name="eventKind">The event, for <see cref="TopRaceKind.EventWins"/>.</param>
    /// <returns>The races which counted it.</returns>
    public int Add(TopRaceParticipant participant, TopRaceKind kind, long amount, DateTime nowUtc, TopRaceEvent eventKind = TopRaceEvent.Any)
    {
        if (amount <= 0 || string.IsNullOrEmpty(participant.Key))
        {
            return 0;
        }

        lock (this.Store.Lock)
        {
            this.Roll(nowUtc);
            var counted = 0;
            foreach (var (race, run) in this.RunningLocked(nowUtc))
            {
                if (race.Kind != kind || kind == TopRaceKind.LevelReached || !Counts(race, participant, eventKind))
                {
                    continue;
                }

                var entry = EntryLocked(run, participant);
                entry.Value += amount;
                entry.UpdatedAtUtc = nowUtc;
                counted++;
                this.Store.Changed();
            }

            return counted;
        }
    }

    /// <summary>
    /// Looks at a character's level and resets: the levels and resets gained since it was last seen count for the races
    /// of levels and resets gained; the level for the races of the level reached. The first look only remembers them.
    /// </summary>
    /// <param name="participant">The character.</param>
    /// <param name="level">The level.</param>
    /// <param name="resets">The resets.</param>
    /// <param name="nowUtc">The time.</param>
    public void Observe(TopRaceParticipant participant, int level, int resets, DateTime nowUtc)
    {
        if (string.IsNullOrEmpty(participant.Key))
        {
            return;
        }

        lock (this.Store.Lock)
        {
            var data = this.Store.Data;
            if (!data.Seen.TryGetValue(participant.Key, out var seen))
            {
                data.Seen[participant.Key] = new TopRaceSeen { Level = level, Resets = resets };
                this.Store.Changed();
            }
            else
            {
                var levels = level - seen.Level;
                var gainedResets = resets - seen.Resets;
                if (levels != 0 || gainedResets != 0)
                {
                    seen.Level = level;
                    seen.Resets = resets;
                    this.Store.Changed();
                }

                if (levels > 0)
                {
                    this.Add(participant, TopRaceKind.LevelsGained, levels, nowUtc);
                }

                if (gainedResets > 0)
                {
                    this.Add(participant, TopRaceKind.ResetsGained, gainedResets, nowUtc);
                }
            }

            this.Roll(nowUtc);
            foreach (var (race, run) in this.RunningLocked(nowUtc))
            {
                if (race.Kind != TopRaceKind.LevelReached || level <= 0 || !Counts(race, participant, TopRaceEvent.Any))
                {
                    continue;
                }

                var entry = EntryLocked(run, participant);
                if (level > entry.Value)
                {
                    entry.Value = level;
                    entry.UpdatedAtUtc = nowUtc;
                    this.Store.Changed();
                }
            }
        }
    }

    /// <summary>Gets the running races (a copy of each run's place in the store).</summary>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The races with their runs.</returns>
    public List<(TopRaceDefinition Race, TopRaceRun Run)> Running(DateTime nowUtc)
    {
        lock (this.Store.Lock)
        {
            this.Roll(nowUtc);
            return this.RunningLocked(nowUtc).ToList();
        }
    }

    /// <summary>Gets the running period of a race.</summary>
    /// <param name="raceId">The race id.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The run, if it runs.</returns>
    public TopRaceRun? Run(string raceId, DateTime nowUtc) => this.Running(nowUtc).FirstOrDefault(r => r.Race.Id == raceId).Run;

    /// <summary>
    /// Gets the custom races which haven't started yet.
    /// </summary>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The races with their periods, soonest first.</returns>
    public List<(TopRaceDefinition Race, TopRaceWindow Window)> Upcoming(DateTime nowUtc) =>
        this.Races.Select(r => (Race: r, Window: this.CustomWindow(r))).Where(r => r.Window is { } w && w.StartUtc > nowUtc)
            .Select(r => (r.Race, r.Window!)).OrderBy(r => r.Item2.StartUtc).ToList();

    /// <summary>
    /// Gets the standings of a table: values above 0, best first; a tie goes to whoever got there first.
    /// </summary>
    /// <param name="race">The race.</param>
    /// <param name="run">The run.</param>
    /// <param name="table">The table's class (-1 for all).</param>
    /// <param name="count">The lines.</param>
    /// <returns>The standings.</returns>
    public List<TopRaceStanding> Standings(TopRaceDefinition race, TopRaceRun run, int table, int count = KeptRanks)
    {
        lock (this.Store.Lock)
        {
            return StandingsLocked(race, run, table, count);
        }
    }

    /// <summary>Gets the rank of a participant in a running race.</summary>
    /// <param name="race">The race.</param>
    /// <param name="run">The run.</param>
    /// <param name="key">The participant's key.</param>
    /// <returns>The rank (0 without a value) and the value.</returns>
    public (int Rank, long Value) RankOf(TopRaceDefinition race, TopRaceRun run, string key)
    {
        lock (this.Store.Lock)
        {
            if (!run.Entries.TryGetValue(key, out var entry))
            {
                return (0, 0);
            }

            var standing = StandingsLocked(race, run, TableOf(race, entry.Class), int.MaxValue).FirstOrDefault(s => s.Entry.Key == key);
            return (standing?.Rank ?? 0, entry.Value);
        }
    }

    /// <summary>Gets the finished races, newest first.</summary>
    /// <param name="nowUtc">The time.</param>
    /// <param name="count">How many.</param>
    /// <returns>The results.</returns>
    public List<TopRaceResult> History(DateTime nowUtc, int count = int.MaxValue)
    {
        lock (this.Store.Lock)
        {
            this.Roll(nowUtc);
            return this.Store.Data.History.Take(count).ToList();
        }
    }

    /// <summary>
    /// Takes the finished races which weren't paid yet; each is paid once (<see cref="MarkPaid"/> after paying).
    /// </summary>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The results, oldest first.</returns>
    public List<TopRaceResult> TakeUnpaid(DateTime nowUtc)
    {
        lock (this.Store.Lock)
        {
            this.Roll(nowUtc);
            return this.Store.Data.History.Where(h => !h.Paid).Reverse().ToList();
        }
    }

    /// <summary>
    /// Takes the starts and ends which weren't announced yet, and marks them as announced.
    /// </summary>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The started runs and the finished results.</returns>
    public (List<(TopRaceDefinition Race, TopRaceRun Run)> Started, List<TopRaceResult> Ended) TakeAnnouncements(DateTime nowUtc)
    {
        lock (this.Store.Lock)
        {
            this.Roll(nowUtc);
            var started = this.RunningLocked(nowUtc).Where(r => !r.Run.StartAnnounced).ToList();
            foreach (var (_, run) in started)
            {
                run.StartAnnounced = true;
            }

            var ended = this.Store.Data.History.Where(h => !h.EndAnnounced).Reverse().ToList();
            foreach (var result in ended)
            {
                result.EndAnnounced = true;
            }

            if (started.Count > 0 || ended.Count > 0)
            {
                this.Store.Changed();
            }

            return (started, ended);
        }
    }

    /// <summary>
    /// Notes a reference of a result as paid; a retry skips it.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <param name="reference">The reference.</param>
    /// <returns><c>true</c>, if it wasn't paid before.</returns>
    public bool NotePaid(TopRaceResult result, string reference)
    {
        lock (this.Store.Lock)
        {
            if (result.PaidReferences.Contains(reference))
            {
                return false;
            }

            result.PaidReferences.Add(reference);
            this.Store.Changed();
            return true;
        }
    }

    /// <summary>Marks a result as paid.</summary>
    /// <param name="result">The result.</param>
    public void MarkPaid(TopRaceResult result)
    {
        lock (this.Store.Lock)
        {
            result.Paid = true;
            this.TrimHistoryLocked();
            this.Store.Changed();
        }
    }

    /// <summary>
    /// Adds a prize which waits for room in the bag; one with the same reference isn't added twice.
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <param name="reward">The prize.</param>
    /// <returns><c>true</c>, if it was added.</returns>
    public bool AddOwed(string characterId, TopRaceOwedReward reward)
    {
        lock (this.Store.Lock)
        {
            var data = this.Store.Data;
            if (!data.Owed.TryGetValue(characterId, out var list))
            {
                list = new List<TopRaceOwedReward>();
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

    /// <summary>Gets the waiting prizes of a character.</summary>
    /// <param name="characterId">The character id.</param>
    /// <returns>The prizes.</returns>
    public List<TopRaceOwedReward> Owed(string characterId)
    {
        lock (this.Store.Lock)
        {
            return this.Store.Data.Owed.TryGetValue(characterId, out var list) ? list.ToList() : new List<TopRaceOwedReward>();
        }
    }

    /// <summary>Takes the waiting prizes of a character (give back what wasn't given with <see cref="ReturnOwed"/>).</summary>
    /// <param name="characterId">The character id.</param>
    /// <returns>The prizes.</returns>
    public List<TopRaceOwedReward> TakeOwed(string characterId)
    {
        lock (this.Store.Lock)
        {
            if (!this.Store.Data.Owed.Remove(characterId, out var list))
            {
                return new List<TopRaceOwedReward>();
            }

            this.Store.Changed();
            return list;
        }
    }

    /// <summary>Puts prizes back which weren't given.</summary>
    /// <param name="characterId">The character id.</param>
    /// <param name="rewards">The prizes.</param>
    public void ReturnOwed(string characterId, IEnumerable<TopRaceOwedReward> rewards)
    {
        foreach (var reward in rewards)
        {
            this.AddOwed(characterId, reward);
        }
    }

    private static bool Counts(TopRaceDefinition race, TopRaceParticipant participant, TopRaceEvent eventKind)
    {
        var isGuild = GuildIdOf(participant.Key) is not null;
        if (race.IsGuildRace != isGuild)
        {
            return false;
        }

        if (race.Kind == TopRaceKind.EventWins && race.Event != TopRaceEvent.Any && race.Event != eventKind)
        {
            return false;
        }

        return race.IsGuildRace || race.Scope != TopRaceScope.OneClass || participant.Class == (int)race.Class;
    }

    private static TopRaceEntry EntryLocked(TopRaceRun run, TopRaceParticipant participant)
    {
        if (!run.Entries.TryGetValue(participant.Key, out var entry))
        {
            entry = new TopRaceEntry { Key = participant.Key };
            run.Entries[participant.Key] = entry;
        }

        if (!string.IsNullOrEmpty(participant.Name))
        {
            entry.Name = participant.Name;
        }

        if (!string.IsNullOrEmpty(participant.Login))
        {
            entry.Login = participant.Login;
        }

        entry.Class = participant.Class;
        return entry;
    }

    private static List<TopRaceStanding> StandingsLocked(TopRaceDefinition race, TopRaceRun run, int table, int count) =>
        run.Entries.Values
            .Where(e => e.Value > 0 && (table < 0 || TableOf(race, e.Class) == table))
            .OrderByDescending(e => e.Value)
            .ThenBy(e => e.UpdatedAtUtc)
            .ThenBy(e => e.Key, StringComparer.Ordinal)
            .Take(count)
            .Select((e, index) => new TopRaceStanding(index + 1, e))
            .ToList();

    private IEnumerable<(TopRaceDefinition Race, TopRaceRun Run)> RunningLocked(DateTime nowUtc)
    {
        foreach (var run in this.Store.Data.Runs.Values.OrderBy(r => r.EndUtc))
        {
            if (run.StartUtc <= nowUtc && nowUtc < run.EndUtc && this.Race(run.RaceId) is { } race)
            {
                yield return (race, run);
            }
        }
    }

    private TopRaceResult Finish(TopRaceDefinition race, TopRaceRun run)
    {
        var result = new TopRaceResult
        {
            RaceId = race.Id, TitleEn = race.TitleEn, TitleVi = race.TitleViOrEn, Kind = race.Kind, Period = race.Period,
            PeriodKey = run.PeriodKey, StartUtc = run.StartUtc, EndUtc = run.EndUtc,
        };
        var keep = Math.Max(KeptRanks, race.Prizes.Select(p => Math.Max(p.RankFrom, p.RankTo)).DefaultIfEmpty(0).Max());
        foreach (var table in TableClasses(race))
        {
            var lines = StandingsLocked(race, run, table, keep);
            if (lines.Count == 0)
            {
                continue;
            }

            var resultTable = new TopRaceTable { Class = table };
            foreach (var standing in lines)
            {
                var prize = PrizeOf(race, standing.Rank, standing.Entry.Value);
                resultTable.Lines.Add(new TopRaceLine
                {
                    Rank = standing.Rank, Key = standing.Entry.Key, Name = standing.Entry.Name, Login = standing.Entry.Login, Class = standing.Entry.Class,
                    Value = standing.Entry.Value, Prized = prize is not null, Coins = prize?.Coins ?? 0, CoinType = prize?.CoinType ?? default,
                    Zen = prize?.Zen ?? 0, Items = prize?.Items.Where(i => i.Count > 0).Select(i => new MissionItemReward { Group = i.Group, Number = i.Number, Level = i.Level, Count = i.Count }).ToList() ?? new(),
                });
            }

            result.Tables.Add(resultTable);
        }

        // Nothing to pay without a prize.
        result.Paid = result.Tables.All(t => t.Lines.All(l => !l.Prized));
        return result;
    }

    private void TrimHistoryLocked()
    {
        var history = this.Store.Data.History;
        var kept = Math.Max(1, this.Configuration.HistoryKept);
        while (history.Count > kept)
        {
            var index = history.FindLastIndex(h => h.Paid && h.EndAnnounced);
            if (index < kept)
            {
                break;
            }

            history.RemoveAt(index);
            this.Store.Changed();
        }
    }
}
