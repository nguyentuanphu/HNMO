// <copyright file="TopRaceEngineTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.GameLogic.PlugIns.TopRaces;

/// <summary>
/// Tests for HNMO's top races: the periods (Sydney days, weeks, months and custom wall-clock dates with daylight
/// saving), the values counted, ties, per-class tables, the end with its prizes paid once, the prizes which wait for
/// room in the bag and the history.
/// </summary>
[TestFixture]
public class TopRaceEngineTest
{
    private static readonly TopRaceParticipant Ann = new("c-ann", "Ann", "ann", (int)TopRaceClass.DarkKnight);
    private static readonly TopRaceParticipant Bob = new("c-bob", "Bob", "bob", (int)TopRaceClass.DarkWizard);
    private static readonly TopRaceParticipant Cat = new("c-cat", "Cat", "cat", (int)TopRaceClass.DarkKnight);

    /// <summary>
    /// Days, weeks (Monday) and months start at 00:00 Sydney time, with daylight saving (UTC+11 in summer, +10 in winter).
    /// </summary>
    [Test]
    public void PeriodsFollowTheSydneyWallClock()
    {
        var engine = Engine(Race("r", TopRaceKind.MonsterKills, TopRacePeriod.Daily));
        var race = engine.Races[0];
        var now = Utc(2026, 11, 15, 5, 0); // Sunday 15 November, 16:00 AEDT

        var day = engine.WindowOf(race, now)!;
        Assert.That(day, Is.EqualTo(new TopRaceWindow("d2026-11-15", Utc(2026, 11, 14, 13, 0), Utc(2026, 11, 15, 13, 0))));

        race.Period = TopRacePeriod.Weekly;
        var week = engine.WindowOf(race, now)!;
        Assert.That(week, Is.EqualTo(new TopRaceWindow("w2026-11-09", Utc(2026, 11, 8, 13, 0), Utc(2026, 11, 15, 13, 0))), "Monday 9 November 00:00 AEDT.");
        Assert.That(engine.WindowOf(race, Utc(2026, 11, 15, 13, 0))!.Key, Is.EqualTo("w2026-11-16"), "Monday 00:00 starts the next week.");

        race.Period = TopRacePeriod.Monthly;
        var october = engine.WindowOf(race, Utc(2026, 10, 10, 0, 0))!;
        Assert.That(october.Key, Is.EqualTo("m2026-10"));
        Assert.That(october.StartUtc, Is.EqualTo(Utc(2026, 9, 30, 14, 0)), "1 October 00:00 AEST (+10).");
        Assert.That(october.EndUtc, Is.EqualTo(Utc(2026, 10, 31, 13, 0)), "1 November 00:00 AEDT (+11).");

        // The week when the clocks go forward (Sunday 4 October 2026, 02:00 -> 03:00): Monday 5 October 00:00 AEDT.
        race.Period = TopRacePeriod.Weekly;
        var dstWeek = engine.WindowOf(race, Utc(2026, 10, 1, 0, 0))!;
        Assert.That(dstWeek.StartUtc, Is.EqualTo(Utc(2026, 9, 27, 14, 0)));
        Assert.That(dstWeek.EndUtc, Is.EqualTo(Utc(2026, 10, 4, 13, 0)), "6 days 23 hours long.");
    }

    /// <summary>
    /// A custom race runs between its wall-clock dates: a start in the skipped hour moves to the first valid minute, a
    /// start in the repeated hour is the first one; before and after, it doesn't run.
    /// </summary>
    [Test]
    public void CustomDatesHandleDaylightSaving()
    {
        var race = Race("c", TopRaceKind.MonsterKills, TopRacePeriod.Custom);
        race.Start = "2026-10-04 02:30"; // doesn't exist in Sydney
        race.End = "2026-04-05 02:30";
        var engine = Engine(race);
        Assert.That(engine.CustomWindow(race), Is.Null, "An end before the start is invalid.");

        race.End = "2026-10-11 20:00";
        var window = engine.CustomWindow(race)!;
        Assert.That(window.StartUtc, Is.EqualTo(Utc(2026, 10, 3, 16, 0)), "03:00 AEDT, the first valid minute.");
        Assert.That(window.EndUtc, Is.EqualTo(Utc(2026, 10, 11, 9, 0)));
        Assert.That(window.Key, Is.EqualTo("c202610040230"));
        Assert.That(engine.WindowOf(race, Utc(2026, 10, 3, 15, 59)), Is.Null, "Not yet.");
        Assert.That(engine.WindowOf(race, Utc(2026, 10, 3, 16, 0)), Is.Not.Null);
        Assert.That(engine.WindowOf(race, Utc(2026, 10, 11, 9, 0)), Is.Null, "Over.");
        Assert.That(engine.Upcoming(Utc(2026, 10, 1, 0, 0)).Select(u => u.Race.Id), Is.EqualTo(new[] { "c" }));

        var zone = engine.Clock.TimeZone;
        Assert.That(TopRaceEngine.LocalToUtc(new DateTime(2026, 4, 5, 2, 30, 0), zone), Is.EqualTo(Utc(2026, 4, 4, 15, 30)), "The first 02:30 (still AEDT).");
        Assert.That(TopRaceEngine.LocalToUtc(new DateTime(2026, 4, 5, 3, 30, 0), zone), Is.EqualTo(Utc(2026, 4, 4, 17, 30)), "AEST after the change.");
        Assert.That(TopRaceEngine.ParseLocal("2026-11-20T09:00"), Is.EqualTo(new DateTime(2026, 11, 20, 9, 0, 0)));
        Assert.That(TopRaceEngine.ParseLocal("tomorrow"), Is.Null);
    }

    /// <summary>
    /// Values add up in the races of their kind; a tie goes to whoever got there first.
    /// </summary>
    [Test]
    public void TiesGoToWhoeverGotThereFirst()
    {
        var engine = Engine(Race("kills", TopRaceKind.MonsterKills, TopRacePeriod.Weekly), Race("pk", TopRaceKind.PkKills, TopRacePeriod.Weekly));
        var now = Utc(2026, 11, 17, 0, 0);
        engine.Add(Bob, TopRaceKind.MonsterKills, 5, now);
        engine.Add(Ann, TopRaceKind.MonsterKills, 5, now.AddMinutes(1));
        engine.Add(Cat, TopRaceKind.MonsterKills, 7, now.AddMinutes(2));
        engine.Add(Cat, TopRaceKind.PkKills, 1, now.AddMinutes(2));
        Assert.That(engine.Add(Ann, TopRaceKind.MonsterKills, 0, now), Is.EqualTo(0), "Nothing counts for 0.");

        var (race, run) = engine.Running(now.AddMinutes(3)).Single(r => r.Race.Id == "kills");
        var standings = engine.Standings(race, run, -1);
        Assert.That(standings.Select(s => (s.Rank, s.Entry.Name, s.Entry.Value)), Is.EqualTo(new[] { (1, "Cat", 7L), (2, "Bob", 5L), (3, "Ann", 5L) }));
        Assert.That(engine.RankOf(race, run, "c-ann"), Is.EqualTo((3, 5L)));
        Assert.That(engine.RankOf(race, run, "nobody"), Is.EqualTo((0, 0L)));

        engine.Add(Ann, TopRaceKind.MonsterKills, 2, now.AddMinutes(4));
        Assert.That(engine.Standings(race, run, -1)[0].Entry.Name, Is.EqualTo("Cat"), "7 = 7: Cat got there first.");
    }

    /// <summary>
    /// A per-class race keeps a table per class; a one-class race counts only that class; event wins can be one event.
    /// </summary>
    [Test]
    public void ClassTablesAndEventFilters()
    {
        var perClass = Race("levels", TopRaceKind.MonsterKills, TopRacePeriod.Weekly);
        perClass.Scope = TopRaceScope.PerClass;
        var knights = Race("knights", TopRaceKind.MonsterKills, TopRacePeriod.Weekly);
        knights.Scope = TopRaceScope.OneClass;
        knights.Class = TopRaceClass.DarkKnight;
        var castle = Race("bc", TopRaceKind.EventWins, TopRacePeriod.Weekly);
        castle.Event = TopRaceEvent.BloodCastle;
        var engine = Engine(perClass, knights, castle);
        var now = Utc(2026, 11, 17, 0, 0);
        engine.Add(Ann, TopRaceKind.MonsterKills, 3, now);
        engine.Add(Bob, TopRaceKind.MonsterKills, 9, now);
        engine.Add(Cat, TopRaceKind.MonsterKills, 4, now);
        engine.Add(Ann, TopRaceKind.EventWins, 1, now, TopRaceEvent.DevilSquare);
        engine.Add(Bob, TopRaceKind.EventWins, 1, now, TopRaceEvent.BloodCastle);

        var running = engine.Running(now);
        var (pRace, pRun) = running.Single(r => r.Race.Id == "levels");
        Assert.That(engine.Standings(pRace, pRun, (int)TopRaceClass.DarkKnight).Select(s => (s.Rank, s.Entry.Name)), Is.EqualTo(new[] { (1, "Cat"), (2, "Ann") }));
        Assert.That(engine.Standings(pRace, pRun, (int)TopRaceClass.DarkWizard).Select(s => (s.Rank, s.Entry.Name)), Is.EqualTo(new[] { (1, "Bob") }));
        Assert.That(engine.RankOf(pRace, pRun, "c-ann").Rank, Is.EqualTo(2), "Ann's rank is in her class's table.");
        Assert.That(TopRaceEngine.TableClasses(pRace), Has.Count.EqualTo(7));

        var (kRace, kRun) = running.Single(r => r.Race.Id == "knights");
        Assert.That(kRun.Entries.Keys, Is.EquivalentTo(new[] { "c-ann", "c-cat" }), "Bob isn't a knight.");
        Assert.That(TopRaceEngine.TableOf(kRace, (int)TopRaceClass.DarkWizard), Is.EqualTo((int)TopRaceClass.DarkKnight));

        var (_, bRun) = running.Single(r => r.Race.Id == "bc");
        Assert.That(bRun.Entries.Keys, Is.EquivalentTo(new[] { "c-bob" }), "Only Blood Castle wins.");
        Assert.That(TopRaceEngine.ClassOf(26), Is.EqualTo((int)TopRaceClass.RageFighter));
        Assert.That(TopRaceEngine.ClassOf(3), Is.EqualTo((int)TopRaceClass.DarkWizard), "The Grand Master is a wizard.");
    }

    /// <summary>
    /// Levels and resets gained count from the last look (a reset doesn't take levels away); the level reached keeps the
    /// highest level.
    /// </summary>
    [Test]
    public void LevelsAndResetsCountFromTheLastLook()
    {
        var engine = Engine(
            Race("resets", TopRaceKind.ResetsGained, TopRacePeriod.Weekly),
            Race("levels", TopRaceKind.LevelsGained, TopRacePeriod.Weekly),
            Race("reached", TopRaceKind.LevelReached, TopRacePeriod.Weekly));
        var now = Utc(2026, 11, 17, 0, 0);
        engine.Observe(Ann, 380, 2, now);
        engine.Observe(Ann, 400, 2, now.AddMinutes(1));
        engine.Observe(Ann, 1, 3, now.AddMinutes(2));
        engine.Observe(Ann, 12, 3, now.AddMinutes(3));

        var running = engine.Running(now.AddMinutes(4)).ToDictionary(r => r.Race.Id, r => r.Run);
        Assert.That(running["resets"].Entries["c-ann"].Value, Is.EqualTo(1));
        Assert.That(running["levels"].Entries["c-ann"].Value, Is.EqualTo(20 + 11), "380 -> 400, then 1 -> 12.");
        Assert.That(running["reached"].Entries["c-ann"].Value, Is.EqualTo(400), "The highest level of the race.");
        Assert.That(engine.Add(Ann, TopRaceKind.LevelReached, 5, now), Is.EqualTo(0), "The level reached isn't added up.");
    }

    /// <summary>
    /// At the end, the tables with their prizes go into the history (the minimum applies), are paid once and the next
    /// period starts empty; the references are unique per race, period, table and character.
    /// </summary>
    [Test]
    public void TheEndPaysOnceWithUniqueReferences()
    {
        var race = Race("weekly", TopRaceKind.BossHuntPoints, TopRacePeriod.Weekly);
        race.Minimum = 10;
        race.Prizes =
        [
            new TopRacePrize { RankFrom = 1, RankTo = 1, Coins = 300, CoinType = CashShopCoinType.WCoinC, Zen = 1000, Items = [new MissionItemReward { Group = 14, Number = 13, Count = 2 }] },
            new TopRacePrize { RankFrom = 2, RankTo = 10, Coins = 30 },
        ];
        var engine = Engine(race);
        var now = Utc(2026, 11, 17, 0, 0);
        engine.Add(Ann, TopRaceKind.BossHuntPoints, 50, now);
        engine.Add(Bob, TopRaceKind.BossHuntPoints, 12, now);
        engine.Add(Cat, TopRaceKind.BossHuntPoints, 9, now);
        Assert.That(engine.TakeAnnouncements(now).Started.Select(s => s.Race.Id), Is.EqualTo(new[] { "weekly" }));
        Assert.That(engine.TakeAnnouncements(now).Started, Is.Empty, "A start is announced once.");
        Assert.That(engine.TakeUnpaid(now), Is.Empty, "It still runs.");

        var after = Utc(2026, 11, 22, 13, 1); // Monday 23 November 00:01 AEDT
        var unpaid = engine.TakeUnpaid(after);
        Assert.That(unpaid, Has.Count.EqualTo(1));
        var result = unpaid[0];
        Assert.That(result.PeriodKey, Is.EqualTo("w2026-11-16"));
        var lines = result.Tables.Single().Lines;
        Assert.That(lines.Select(l => (l.Rank, l.Name, l.Prized, l.Coins)), Is.EqualTo(new[] { (1, "Ann", true, 300), (2, "Bob", true, 30), (3, "Cat", false, 0) }), "Cat has less than the minimum.");
        Assert.That(lines[0].CoinType, Is.EqualTo(CashShopCoinType.WCoinC));
        Assert.That(lines[0].Items.Single().Count, Is.EqualTo(2));

        var grant = TopRaceEngine.PrizeGrant(result, -1, lines[0], "ann")!;
        Assert.That(grant.Reference, Is.EqualTo("hnmo-top-race:weekly:w2026-11-16:-1:c-ann"));
        Assert.That(grant.Amount, Is.EqualTo(300));
        Assert.That(TopRaceEngine.PrizeGrant(result, -1, lines[2], "cat"), Is.Null);
        var member = Guid.NewGuid();
        Assert.That(TopRaceEngine.PrizeReference(result, -1, "guild:x", member), Does.EndWith(":" + member.ToString("N")));

        Assert.That(engine.NotePaid(result, grant.Reference), Is.True);
        Assert.That(engine.NotePaid(result, grant.Reference), Is.False, "A retry skips a paid reference.");
        engine.MarkPaid(result);
        Assert.That(engine.TakeUnpaid(after), Is.Empty);
        Assert.That(engine.TakeAnnouncements(after).Ended.Select(e => e.RaceId), Is.EqualTo(new[] { "weekly" }));

        var (_, next) = engine.Running(after).Single();
        Assert.That(next.PeriodKey, Is.EqualTo("w2026-11-23"));
        Assert.That(next.Entries, Is.Empty, "The new week starts empty.");
        Assert.That(engine.History(after).Single().PeriodKey, Is.EqualTo("w2026-11-16"));
    }

    /// <summary>
    /// A race without anyone or without prizes ends as paid; a race turned off ends without a result.
    /// </summary>
    [Test]
    public void EmptyAndDisabledRaces()
    {
        var daily = Race("daily", TopRaceKind.MonsterKills, TopRacePeriod.Daily);
        var off = Race("off", TopRaceKind.MonsterKills, TopRacePeriod.Daily);
        var engine = Engine(daily, off);
        var now = Utc(2026, 11, 17, 0, 0);
        engine.Add(Ann, TopRaceKind.MonsterKills, 3, now);
        off.Enabled = false;
        var tomorrow = now.AddDays(1);
        Assert.That(engine.TakeUnpaid(tomorrow), Is.Empty, "No prizes: nothing to pay.");
        var history = engine.History(tomorrow);
        Assert.That(history.Select(h => h.RaceId), Is.EqualTo(new[] { "daily" }));
        Assert.That(history[0].Paid, Is.True);
        Assert.That(engine.Running(tomorrow).Select(r => r.Race.Id), Is.EqualTo(new[] { "daily" }));
    }

    /// <summary>
    /// A guild race ranks guilds; characters don't count there and guilds don't count elsewhere.
    /// </summary>
    [Test]
    public void GuildRacesRankGuilds()
    {
        var engine = Engine(Race("guilds", TopRaceKind.GuildPoints, TopRacePeriod.Weekly), Race("kills", TopRaceKind.MonsterKills, TopRacePeriod.Weekly));
        var now = Utc(2026, 11, 17, 0, 0);
        var guild = Guid.NewGuid();
        var key = TopRaceEngine.GuildKey(guild);
        Assert.That(TopRaceEngine.GuildIdOf(key), Is.EqualTo(guild));
        Assert.That(TopRaceEngine.GuildIdOf("c-ann"), Is.Null);
        Assert.That(engine.Add(new TopRaceParticipant(key, "Alpha", string.Empty, -1), TopRaceKind.GuildPoints, 40, now), Is.EqualTo(1));
        Assert.That(engine.Add(Ann, TopRaceKind.GuildPoints, 40, now), Is.EqualTo(0));
        Assert.That(engine.Add(new TopRaceParticipant(key, "Alpha", string.Empty, -1), TopRaceKind.MonsterKills, 1, now), Is.EqualTo(0));
        var (race, run) = engine.Running(now).Single(r => r.Race.Id == "guilds");
        Assert.That(engine.Standings(race, run, -1).Single().Entry.Name, Is.EqualTo("Alpha"));
    }

    /// <summary>
    /// Prizes wait for room in the bag, once per reference; what wasn't given goes back.
    /// </summary>
    [Test]
    public void OwedPrizesWaitOnce()
    {
        var engine = Engine();
        var reward = new TopRaceOwedReward { Reference = "r1", Rank = 1, Reward = new MissionReward { Zen = 10 } };
        Assert.That(engine.AddOwed("c-ann", reward), Is.True);
        Assert.That(engine.AddOwed("c-ann", reward), Is.False);
        var taken = engine.TakeOwed("c-ann");
        Assert.That(taken, Has.Count.EqualTo(1));
        Assert.That(engine.Owed("c-ann"), Is.Empty);
        engine.ReturnOwed("c-ann", taken);
        Assert.That(engine.Owed("c-ann").Single().Reference, Is.EqualTo("r1"));
    }

    /// <summary>
    /// The state outlasts a restart (the file), and the JSON for the game and the website is plain ASCII.
    /// </summary>
    [Test]
    public void TheStateIsSavedAndWritten()
    {
        var file = Path.Combine(Path.GetTempPath(), $"hnmo-top-races-{Guid.NewGuid():N}.json");
        try
        {
            var race = Race("weekly", TopRaceKind.MonsterKills, TopRacePeriod.Weekly);
            race.TitleVi = "Đua top tuần";
            var configuration = new TopRacesConfiguration { Races = [race] };
            var store = new TopRaceStore(file);
            var engine = new TopRaceEngine(configuration, store);
            var now = Utc(2026, 11, 17, 0, 0);
            engine.Add(Ann, TopRaceKind.MonsterKills, 4, now);
            lock (store.Lock)
            {
                store.SaveIfChanged();
            }

            var reloaded = new TopRaceEngine(configuration, new TopRaceStore(file));
            Assert.That(reloaded.Running(now).Single().Run.Entries["c-ann"].Value, Is.EqualTo(4));

            var state = TopRaceStateWriter.WriteState(reloaded, "c-ann", Ann.Class, null, now, new TopRaceStateEvent("start", TextVi: "Đua"));
            Assert.That(state.All(c => c < 128), Is.True, "Plain ASCII.");
            using var json = JsonDocument.Parse(state);
            var first = json.RootElement.GetProperty("races")[0];
            Assert.That(first.GetProperty("myRank").GetInt32(), Is.EqualTo(1));
            Assert.That(first.GetProperty("race").GetProperty("titleVi").GetString(), Is.EqualTo("Đua top tuần"));
            Assert.That(first.GetProperty("top")[0].GetProperty("name").GetString(), Is.EqualTo("Ann"));

            var website = TopRaceStateWriter.WritePublic(reloaded, now);
            Assert.That(website, Does.Not.Contain("\"ann\""), "No login names on the website.");
        }
        finally
        {
            File.Delete(file);
        }
    }

    /// <summary>
    /// The defaults: a few races with modest prizes, the PK race off.
    /// </summary>
    [Test]
    public void TheDefaultsAreModest()
    {
        var configuration = new TopRacesConfiguration();
        Assert.That(configuration.Races.Select(r => r.Id), Is.Unique);
        Assert.That(configuration.Races.Single(r => r.Kind == TopRaceKind.PkKills).Enabled, Is.False);
        Assert.That(configuration.Races.Single(r => r.Id == "monthly-levels").Scope, Is.EqualTo(TopRaceScope.PerClass));
        Assert.That(configuration.Races.SelectMany(r => r.Prizes).Max(p => p.Coins), Is.LessThanOrEqualTo(300));
    }

    private static TopRaceDefinition Race(string id, TopRaceKind kind, TopRacePeriod period) =>
        new() { Id = id, TitleEn = id, Kind = kind, Period = period, Prizes = [new TopRacePrize { RankFrom = 1, RankTo = 3, Coins = 0 }] };

    private static TopRaceEngine Engine(params TopRaceDefinition[] races) =>
        new(new TopRacesConfiguration { Races = races.ToList() }, new TopRaceStore());

    private static DateTime Utc(int year, int month, int day, int hour, int minute) => new(year, month, day, hour, minute, 0, DateTimeKind.Utc);
}
