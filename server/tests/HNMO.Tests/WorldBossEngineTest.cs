// <copyright file="WorldBossEngineTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.GameLogic.PlugIns.WorldBosses;

/// <summary>
/// Tests for HNMO's world bosses: the schedule in Sydney time (with daylight saving), the plan of announcements,
/// spawns and despawns, the damage ranking, the reward tiers, the boss-hunt points and the weekly rankings which pay once.
/// </summary>
[TestFixture]
public class WorldBossEngineTest
{
    private static readonly TimeZoneInfo Sydney = new MissionClock("Australia/Sydney", 0).TimeZone;

    /// <summary>
    /// A fixed time stays at the same Sydney wall-clock time in summer (UTC+11) and winter (UTC+10).
    /// </summary>
    [Test]
    public void FixedTimesFollowSydneyTimeInSummerAndWinter()
    {
        var boss = new WorldBossDefinition { Id = "b", ScheduleKind = WorldBossScheduleKind.FixedTimes, FixedTimes = "20:00, 12:00" };
        Assert.That(WorldBossSchedule.Next(boss, Sydney, Utc(2026, 11, 15, 0, 0)), Is.EqualTo(Utc(2026, 11, 15, 1, 0)), "12:00 Sydney summer = 01:00 UTC.");
        Assert.That(WorldBossSchedule.Next(boss, Sydney, Utc(2026, 11, 15, 1, 0)), Is.EqualTo(Utc(2026, 11, 15, 9, 0)), "20:00 Sydney summer = 09:00 UTC; the time itself isn't 'after'.");
        Assert.That(WorldBossSchedule.Next(boss, Sydney, Utc(2026, 6, 15, 3, 0)), Is.EqualTo(Utc(2026, 6, 15, 10, 0)), "20:00 Sydney winter = 10:00 UTC.");
    }

    /// <summary>
    /// When the clocks go forward (4 October 2026, 02:00 -> 03:00), 02:30 doesn't exist and moves to 03:00; when they go
    /// back (5 April 2026, 03:00 -> 02:00), 02:30 happens twice and counts once.
    /// </summary>
    [Test]
    public void DaylightSavingDaysMoveOrMergeTheTime()
    {
        var boss = new WorldBossDefinition { Id = "b", ScheduleKind = WorldBossScheduleKind.FixedTimes, FixedTimes = "02:30" };
        Assert.That(WorldBossSchedule.Next(boss, Sydney, Utc(2026, 10, 3, 10, 0)), Is.EqualTo(Utc(2026, 10, 3, 16, 0)), "03:00 AEDT on the 4th.");
        var first = WorldBossSchedule.Next(boss, Sydney, Utc(2026, 4, 4, 10, 0))!.Value;
        Assert.That(first, Is.EqualTo(Utc(2026, 4, 4, 15, 30)), "02:30 AEDT (the first one) on the 5th.");
        var second = WorldBossSchedule.Next(boss, Sydney, first)!.Value;
        Assert.That(second, Is.EqualTo(Utc(2026, 4, 5, 16, 30)), "The next is 02:30 AEST on the 6th, not the repeated hour.");
    }

    /// <summary>
    /// "Every N hours" starts at the start time each day; the days of the week filter.
    /// </summary>
    [Test]
    public void EveryHoursAndDaysOfTheWeek()
    {
        var boss = new WorldBossDefinition { Id = "b", ScheduleKind = WorldBossScheduleKind.EveryHours, EveryHours = 4, StartTime = "02:00" };
        Assert.That(WorldBossSchedule.FormatTimes(WorldBossSchedule.TimesOfDay(boss)), Is.EqualTo("02:00, 06:00, 10:00, 14:00, 18:00, 22:00"));

        // Sunday 15 Nov 2026 23:00 Sydney -> next is Monday 02:00; with Saturday only, the next Saturday 02:00.
        var sundayNight = Utc(2026, 11, 15, 12, 0);
        Assert.That(WorldBossSchedule.Next(boss, Sydney, sundayNight), Is.EqualTo(Utc(2026, 11, 15, 15, 0)));
        boss.Days = [DayOfWeek.Saturday];
        Assert.That(WorldBossSchedule.Next(boss, Sydney, sundayNight), Is.EqualTo(Utc(2026, 11, 20, 15, 0)), "Saturday 21 Nov 02:00 Sydney.");

        Assert.That(WorldBossSchedule.TryParseTimes("8:05; 20:30", out var times), Is.True);
        Assert.That(WorldBossSchedule.FormatTimes(times), Is.EqualTo("08:05, 20:30"));
        Assert.That(WorldBossSchedule.TryParseTimes("25:00", out _), Is.False);
    }

    /// <summary>
    /// The plan announces once before the spawn, spawns once, despawns when the time is over, and doesn't spawn a boss
    /// whose time passed long ago (the server was off).
    /// </summary>
    [Test]
    public void PlanAnnouncesSpawnsAndDespawnsOnce()
    {
        var boss = new WorldBossDefinition { Id = "dragon", ScheduleKind = WorldBossScheduleKind.FixedTimes, FixedTimes = "20:00", AnnounceMinutesBefore = 10, DespawnMinutes = 30 };
        var engine = Engine(boss);
        var spawn = Utc(2026, 11, 15, 9, 0);

        Assert.That(engine.Plan([boss], 0, spawn.AddMinutes(-30)), Is.Empty);
        Assert.That(engine.Plan([boss], 0, spawn.AddMinutes(-9)).Select(a => a.Kind), Is.EqualTo(new[] { WorldBossActionKind.Soon }));
        Assert.That(engine.Plan([boss], 0, spawn.AddMinutes(-8)), Is.Empty, "Announced once.");
        var actions = engine.Plan([boss], 0, spawn.AddSeconds(5));
        Assert.That(actions.Select(a => a.Kind), Is.EqualTo(new[] { WorldBossActionKind.Spawn }));
        Assert.That(actions[0].AtUtc, Is.EqualTo(spawn));
        Assert.That(engine.Plan([boss], 0, spawn.AddSeconds(10)), Is.Empty, "Spawned once.");
        Assert.That(engine.NextSpawn(boss, spawn.AddSeconds(10)), Is.EqualTo(spawn.AddDays(1)));

        var alive = engine.RegisterSpawn(boss, 0, 100, 120, 50_000, spawn.AddSeconds(5));
        Assert.That(engine.Plan([boss], 0, spawn.AddMinutes(29)), Is.Empty);
        Assert.That(engine.Plan([boss], 1, spawn.AddMinutes(31)), Is.Empty, "Another game server doesn't despawn it.");
        var despawn = engine.Plan([boss], 0, spawn.AddMinutes(31));
        Assert.That(despawn.Select(a => (a.Kind, a.InstanceId)), Is.EqualTo(new[] { (WorldBossActionKind.Despawn, (string?)alive.InstanceId) }));
        Assert.That(engine.Escape(alive.InstanceId, spawn.AddMinutes(31))!.Killed, Is.False);

        // The server was off for the next spawn (more than the despawn minutes): it isn't spawned late.
        Assert.That(engine.Plan([boss], 0, spawn.AddDays(1).AddMinutes(45)).Where(a => a.Kind == WorldBossActionKind.Spawn), Is.Empty);
        Assert.That(engine.NextSpawn(boss, spawn.AddDays(1).AddMinutes(45)), Is.EqualTo(spawn.AddDays(2)));
    }

    /// <summary>
    /// The damage ranks the players; ranks 1, 2, 3 and 4-10 get their tiers, the others with enough damage the
    /// participation reward, the last hit its bonus; players without damage get nothing. Boss-hunt points are shared
    /// by the damage, at least 1.
    /// </summary>
    [Test]
    public void KillRanksTheDamageAndGivesTheTiers()
    {
        var boss = new WorldBossDefinition
        {
            Id = "dragon",
            NameEn = "Red Dragon",
            HuntPoints = 100,
            ParticipationMinimumPercent = 1,
            Top1 = new MissionReward { Coins = 50 },
            Top2 = new MissionReward { Coins = 30 },
            Top3 = new MissionReward { Coins = 20 },
            Top10 = new MissionReward { Zen = 1000 },
            Participation = new MissionReward { Coins = 2, Items = [new MissionItemReward { Group = 14, Number = 13 }] },
            LastHit = new MissionReward { Coins = 10 },
        };
        var engine = Engine(boss);
        var now = Utc(2026, 11, 15, 9, 0);
        var alive = engine.RegisterSpawn(boss, 0, 1, 1, 1_000_000, now);

        // p01 does the most, ... p12 the least; p13 0.5 % only; p14 no damage.
        for (var i = 1; i <= 12; i++)
        {
            Assert.That(engine.RecordDamage(alive.InstanceId, $"c{i:00}", $"p{i:00}", $"login{i:00}", null, 10_000 - (i * 100)), Is.True);
        }

        engine.RecordDamage(alive.InstanceId, "c13", "p13", "login13", null, 500);
        Assert.That(engine.RecordDamage(alive.InstanceId, "c14", "p14", "login14", null, 0), Is.False, "No damage, no entry.");
        Assert.That(engine.RecordDamage(alive.InstanceId, "c01", "p01", "login01", null, 5_000_000), Is.True);
        Assert.That(engine.Alive(alive.InstanceId)!.Damage["c01"].Damage, Is.EqualTo(9_900 + 1_000_000), "A hit counts at most the maximum health.");

        var outcome = engine.Kill(alive.InstanceId, "c05", now.AddMinutes(3))!;
        Assert.That(engine.Kill(alive.InstanceId, "c05", now.AddMinutes(3)), Is.Null, "A boss dies once.");
        string[] Tiers(string id) => outcome.Rewards.TryGetValue(id, out var list) ? list.Select(r => r.Tier).ToArray() : [];
        Assert.That(Tiers("c01"), Is.EqualTo(new[] { "top1" }));
        Assert.That(Tiers("c02"), Is.EqualTo(new[] { "top2" }));
        Assert.That(Tiers("c03"), Is.EqualTo(new[] { "top3" }));
        Assert.That(Tiers("c05"), Is.EqualTo(new[] { "top10", "lasthit" }));
        Assert.That(Tiers("c10"), Is.EqualTo(new[] { "top10" }));
        Assert.That(Tiers("c11"), Is.Empty, "Rank 11 has less than 1 % of all damage (c01 did most).");
        Assert.That(Tiers("c14"), Is.Empty);
        Assert.That(outcome.Record.LastHit, Is.EqualTo("p05"));
        Assert.That(outcome.Record.Lines.Select(l => l.Name).Take(3), Is.EqualTo(new[] { "p01", "p02", "p03" }));
        Assert.That(outcome.Rewards["c01"][0].Reference, Is.EqualTo($"hnmo-boss:{alive.InstanceId}:c01:top1"));
        Assert.That(engine.Owed("c05"), Has.Count.EqualTo(2));
        Assert.That(engine.Kills()[0].Killed, Is.True);

        var ranking = engine.Ranking(WeeklyCategory.BossHunt, now.AddMinutes(3), 20);
        Assert.That(ranking[0].Entry.Name, Is.EqualTo("p01"));
        Assert.That(ranking[0].Entry.Points, Is.EqualTo(91), "1,009,900 of 1,112,700 damage, of 100 points.");
        Assert.That(ranking.Single(r => r.Entry.Name == "p13").Entry.Points, Is.EqualTo(1), "At least 1 point for any damage.");
        Assert.That(ranking.Any(r => r.Entry.Name == "p14"), Is.False);
    }

    /// <summary>
    /// The participation reward needs the share or the damage; with a smaller share everyone outside the top 10 gets it.
    /// </summary>
    [Test]
    public void ParticipationNeedsTheShareOrTheDamage()
    {
        var boss = new WorldBossDefinition { Id = "b", ParticipationMinimumPercent = 50, ParticipationMinimumDamage = 300, Participation = new MissionReward { Coins = 1 }, TopAlsoGetParticipation = true };
        var engine = Engine(boss);
        var now = Utc(2026, 11, 15, 9, 0);
        var alive = engine.RegisterSpawn(boss, 0, 1, 1, 10_000, now);
        engine.RecordDamage(alive.InstanceId, "a", "A", "la", null, 1000);
        engine.RecordDamage(alive.InstanceId, "b", "B", "lb", null, 300);
        engine.RecordDamage(alive.InstanceId, "c", "C", "lc", null, 100);
        var outcome = engine.Kill(alive.InstanceId, null, now)!;
        Assert.That(outcome.Rewards.Keys, Is.EquivalentTo(new[] { "a", "b" }), "Top rewards are empty here; a has the share, b the damage, c neither.");
    }

    /// <summary>
    /// In party mode a party's damage counts together; every member who did damage gets the party's rank.
    /// </summary>
    [Test]
    public void PartyModeRanksThePartyTogether()
    {
        var boss = new WorldBossDefinition { Id = "b", RankBy = WorldBossRankBy.Party, Top1 = new MissionReward { Coins = 9 }, Top2 = new MissionReward { Coins = 5 } };
        var engine = Engine(boss);
        var now = Utc(2026, 11, 15, 9, 0);
        var alive = engine.RegisterSpawn(boss, 0, 1, 1, 10_000, now);
        engine.RecordDamage(alive.InstanceId, "solo", "Solo", "l1", null, 1000);
        engine.RecordDamage(alive.InstanceId, "m1", "M1", "l2", "Leader", 600);
        engine.RecordDamage(alive.InstanceId, "m2", "M2", "l3", "Leader", 600);
        var outcome = engine.Kill(alive.InstanceId, null, now)!;
        Assert.That(outcome.Rewards["m1"].Single().Tier, Is.EqualTo("top1"));
        Assert.That(outcome.Rewards["m2"].Single().Tier, Is.EqualTo("top1"));
        Assert.That(outcome.Rewards["solo"].Single().Tier, Is.EqualTo("top2"));
    }

    /// <summary>
    /// A boss which leaves gives no rewards and no points.
    /// </summary>
    [Test]
    public void EscapeGivesNothing()
    {
        var boss = new WorldBossDefinition { Id = "b", Top1 = new MissionReward { Coins = 9 } };
        var engine = Engine(boss);
        var now = Utc(2026, 11, 15, 9, 0);
        var alive = engine.RegisterSpawn(boss, 0, 1, 1, 10_000, now);
        engine.RecordDamage(alive.InstanceId, "a", "A", "la", null, 1000);
        var record = engine.Escape(alive.InstanceId, now.AddMinutes(30))!;
        Assert.That(record.Killed, Is.False);
        Assert.That(record.Lines.Single().Name, Is.EqualTo("A"));
        Assert.That(engine.Owed("a"), Is.Empty);
        Assert.That(engine.Ranking(WeeklyCategory.BossHunt, now), Is.Empty);
    }

    /// <summary>
    /// Rewards which can't be given go back and are taken once.
    /// </summary>
    [Test]
    public void OwedRewardsAreTakenOnce()
    {
        var boss = new WorldBossDefinition { Id = "b", Top1 = new MissionReward { Zen = 5 } };
        var engine = Engine(boss);
        var now = Utc(2026, 11, 15, 9, 0);
        var alive = engine.RegisterSpawn(boss, 0, 1, 1, 10_000, now);
        engine.RecordDamage(alive.InstanceId, "a", "A", "la", null, 1000);
        engine.Kill(alive.InstanceId, null, now);

        var taken = engine.TakeOwed("a");
        Assert.That(taken, Has.Count.EqualTo(1));
        Assert.That(engine.TakeOwed("a"), Is.Empty, "A second caller gets nothing while the first gives it.");
        engine.ReturnOwed("a", taken);
        engine.ReturnOwed("a", taken);
        Assert.That(engine.Owed("a"), Has.Count.EqualTo(1), "Returned once, even twice.");
    }

    /// <summary>
    /// The week starts on Monday 00:00 Sydney; the finished week's top 10 wait to be paid with unique references, and a
    /// week is paid once.
    /// </summary>
    [Test]
    public void WeeklyRankingsRollAtMondayAndPayOnce()
    {
        var configuration = new WorldBossesConfiguration { Bosses = [], FileName = string.Empty };
        configuration.BloodCastle.Ranks = WeeklyRankingConfiguration.DefaultRanks(100, 50, 25, 5);
        var engine = new WorldBossEngine(configuration, new WorldBossStore());
        var sunday = Utc(2026, 11, 15, 12, 0); // Sunday 23:00 Sydney, the week of Monday the 9th.
        for (var i = 1; i <= 12; i++)
        {
            engine.AddPoints(WeeklyCategory.BloodCastle, $"c{i}", $"p{i}", $"l{i}", 100 - i, sunday);
        }

        engine.AddPoints(WeeklyCategory.BossHunt, "c1", "p1", "l1", 7, sunday);
        engine.SetCastleSiegeWinner(Guid.Empty, "Winners", sunday);
        Assert.That(engine.TakeUnpaidWeeks(sunday), Is.Empty, "The week isn't over.");

        var monday = Utc(2026, 11, 15, 13, 0); // Monday 00:00 Sydney.
        var weeks = engine.TakeUnpaidWeeks(monday);
        Assert.That(weeks, Has.Count.EqualTo(1));
        var week = weeks[0];
        Assert.That(week.Week, Is.EqualTo("2026-11-09"));
        Assert.That(week.Winners[WeeklyCategory.BloodCastle], Has.Count.EqualTo(10), "The top 10.");
        Assert.That(week.CastleSiegeGuild, Is.EqualTo("Winners"));
        var grants = WorldBossEngine.RankingGrants(week);
        Assert.That(grants.Where(g => g.Reference.Contains("BloodCastle", StringComparison.Ordinal)).Select(g => g.Amount), Is.EqualTo(new[] { 100, 50, 25, 5, 5, 5, 5, 5, 5, 5 }));
        Assert.That(grants.Select(g => g.Reference).Distinct().Count(), Is.EqualTo(grants.Count));
        Assert.That(grants.Single(g => g.Reference.Contains("BossHunt", StringComparison.Ordinal)).Reference, Is.EqualTo("hnmo-week:2026-11-09:BossHunt:c1"));
        Assert.That(engine.Ranking(WeeklyCategory.BloodCastle, monday), Is.Empty, "The new week starts empty.");

        Assert.That(engine.TakeUnpaidWeeks(monday.AddMinutes(1)), Has.Count.EqualTo(1), "Not paid yet: it's handed out again.");
        engine.MarkPaid(week);
        Assert.That(engine.TakeUnpaidWeeks(monday.AddMinutes(2)), Is.Empty, "Paid once.");
        Assert.That(engine.Store.Data.LastWeek!.Week, Is.EqualTo("2026-11-09"));
    }

    /// <summary>
    /// The store keeps the state in its file over a restart, and the default configuration is valid.
    /// </summary>
    [Test]
    public void StoreSurvivesARestart()
    {
        var file = Path.Combine(Path.GetTempPath(), $"hnmo-bosses-{Guid.NewGuid():N}.json");
        try
        {
            var configuration = new WorldBossesConfiguration();
            var boss = configuration.Bosses.First();
            var engine = new WorldBossEngine(configuration, new WorldBossStore(file));
            var now = Utc(2026, 11, 15, 9, 0);
            var alive = engine.RegisterSpawn(boss, 0, 1, 1, 10_000, now);
            engine.RecordDamage(alive.InstanceId, "a", "A", "la", null, 1000);
            engine.Kill(alive.InstanceId, "a", now);
            lock (engine.Store.Lock)
            {
                engine.Store.SaveIfChanged();
            }

            var again = new WorldBossEngine(configuration, new WorldBossStore(file));
            Assert.That(again.Kills().Single().LastHit, Is.EqualTo("A"));
            Assert.That(again.Owed("a").Select(o => o.Tier), Is.EquivalentTo(new[] { "top1", "lasthit" }));
            Assert.That(again.PointsOf(WeeklyCategory.BossHunt, "a", now), Is.EqualTo((10L, 1)));

            var json = JsonSerializer.Serialize(configuration);
            Assert.That(JsonSerializer.Deserialize<WorldBossesConfiguration>(json)!.Bosses.Select(b => b.Id), Is.EqualTo(new[] { "ice-queen", "red-dragon", "dark-phoenix" }));
            Assert.That(WorldBossBoardWriter.WritePublic(again, now), Does.Contain("\"lastKill\""));
            Assert.That(WorldBossBoardWriter.WriteBoard(again, "a", now, m => "Map", null), Does.Contain("\"huntPoints\":10"));
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static DateTime Utc(int year, int month, int day, int hour, int minute) => new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    private static WorldBossEngine Engine(params WorldBossDefinition[] bosses) =>
        new(new WorldBossesConfiguration { Bosses = bosses.ToList(), FileName = string.Empty }, new WorldBossStore());
}
