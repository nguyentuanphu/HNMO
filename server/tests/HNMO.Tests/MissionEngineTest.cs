// <copyright file="MissionEngineTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// Tests for HNMO's missions and earned coins: progress, the resets at the Sydney hour, the streak, the caps,
/// claims which count once and the invite reward.
/// </summary>
[TestFixture]
public class MissionEngineTest
{
    private const string Hero = "char-1";
    private const string Login = "test8";

    /// <summary>2026-11-15 10:00 in Sydney (daylight saving time, UTC+11).</summary>
    private static readonly DateTime SundayMorning = new(2026, 11, 14, 23, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// The game day changes at midnight Sydney time (13:00 UTC in summer), the week on Monday; a reset hour moves both.
    /// </summary>
    [Test]
    public void DaysAndWeeksChangeAtTheSydneyResetHour()
    {
        var clock = new MissionClock("Australia/Sydney", 0);
        Assert.That(clock.DayKey(new DateTime(2026, 11, 15, 12, 59, 0, DateTimeKind.Utc)), Is.EqualTo("2026-11-15"));
        Assert.That(clock.DayKey(new DateTime(2026, 11, 15, 13, 0, 0, DateTimeKind.Utc)), Is.EqualTo("2026-11-16"));
        Assert.That(clock.WeekKey(new DateTime(2026, 11, 15, 12, 59, 0, DateTimeKind.Utc)), Is.EqualTo("2026-11-09"), "Sunday still belongs to the week of Monday the 9th.");
        Assert.That(clock.WeekKey(new DateTime(2026, 11, 15, 13, 0, 0, DateTimeKind.Utc)), Is.EqualTo("2026-11-16"), "Monday 00:00 Sydney starts a new week.");
        Assert.That(clock.NextDayStart(SundayMorning), Is.EqualTo(new DateTime(2026, 11, 15, 13, 0, 0, DateTimeKind.Utc)));

        var six = new MissionClock("Australia/Sydney", 6);
        Assert.That(six.DayKey(new DateTime(2026, 11, 15, 18, 59, 0, DateTimeKind.Utc)), Is.EqualTo("2026-11-15"), "05:59 on the 16th is still the 15th's game day.");
        Assert.That(six.DayKey(new DateTime(2026, 11, 15, 19, 0, 0, DateTimeKind.Utc)), Is.EqualTo("2026-11-16"));

        // Winter (standard time, UTC+10).
        Assert.That(clock.DayKey(new DateTime(2026, 6, 15, 13, 59, 0, DateTimeKind.Utc)), Is.EqualTo("2026-06-15"));
        Assert.That(clock.DayKey(new DateTime(2026, 6, 15, 14, 0, 0, DateTimeKind.Utc)), Is.EqualTo("2026-06-16"));
    }

    /// <summary>
    /// Kills count only for missions whose monster and map match; a mission finishes once, at its target.
    /// </summary>
    [Test]
    public void KillsCountForMatchingMissionsAndFinishOnce()
    {
        var engine = Engine(new MissionDefinition { Id = "any", Kind = MissionKind.KillMonsters, Target = 3 }, new MissionDefinition { Id = "map3-bull", Kind = MissionKind.KillMonsters, Target = 2, MonsterNumber = 8, MapNumber = 3 });
        bool Kill(MissionDefinition m, short monster, short map) => (m.MonsterNumber <= 0 || m.MonsterNumber == monster) && (m.MapNumber < 0 || m.MapNumber == map);

        Assert.That(engine.AddProgress(Hero, MissionKind.KillMonsters, 1, m => Kill(m, 8, 0), SundayMorning), Is.Empty);
        Assert.That(engine.AddProgress(Hero, MissionKind.KillMonsters, 1, m => Kill(m, 8, 3), SundayMorning), Is.Empty);
        var finished = engine.AddProgress(Hero, MissionKind.KillMonsters, 1, m => Kill(m, 8, 3), SundayMorning);
        Assert.That(finished.Select(m => m.Id), Is.EquivalentTo(new[] { "any", "map3-bull" }));
        Assert.That(engine.AddProgress(Hero, MissionKind.KillMonsters, 1, m => Kill(m, 8, 3), SundayMorning), Is.Empty, "Finished missions don't finish again.");
        Assert.That(engine.GetProgress(Hero, engine.Configuration.Missions.First(), SundayMorning).Value, Is.EqualTo(3), "The count stops at the target.");
        Assert.That(engine.AddProgress(Hero, MissionKind.PickUpZen, 5000, null, SundayMorning), Is.Empty, "Other kinds don't count.");
    }

    /// <summary>
    /// Daily progress starts again at the reset hour, weekly progress on Monday.
    /// </summary>
    [Test]
    public void ProgressResetsDailyAndWeekly()
    {
        var daily = new MissionDefinition { Id = "d", Kind = MissionKind.PickUpZen, Target = 1000 };
        var weekly = new MissionDefinition { Id = "w", Kind = MissionKind.PickUpZen, Target = 100_000, Period = MissionPeriod.Weekly };
        var engine = Engine(daily, weekly);
        engine.AddProgress(Hero, MissionKind.PickUpZen, 500, null, SundayMorning);
        var beforeMidnight = new DateTime(2026, 11, 15, 12, 59, 0, DateTimeKind.Utc);
        var afterMidnight = new DateTime(2026, 11, 15, 13, 1, 0, DateTimeKind.Utc);
        Assert.That(engine.GetProgress(Hero, daily, beforeMidnight).Value, Is.EqualTo(500));
        Assert.That(engine.GetProgress(Hero, daily, afterMidnight).Value, Is.EqualTo(0), "A new day at midnight Sydney time.");
        Assert.That(engine.GetProgress(Hero, weekly, afterMidnight).Value, Is.EqualTo(0), "Sunday -> Monday also starts a new week.");

        engine.AddProgress(Hero, MissionKind.PickUpZen, 700, null, afterMidnight);
        var tuesday = afterMidnight.AddDays(1);
        Assert.That(engine.GetProgress(Hero, daily, tuesday).Value, Is.EqualTo(0));
        Assert.That(engine.GetProgress(Hero, weekly, tuesday).Value, Is.EqualTo(700), "The week goes on.");
    }

    /// <summary>
    /// A reward is claimed once: not before the mission is finished, and not twice in the same period;
    /// a claim which couldn't be given (bag full) stays claimable. The next period can be claimed again with another reference.
    /// </summary>
    [Test]
    public void RewardIsClaimedOnlyOnce()
    {
        var engine = Engine(new MissionDefinition { Id = "lvl", Kind = MissionKind.LevelUp, Target = 2 });
        Assert.That(engine.CheckClaim(Hero, "lvl", SundayMorning, out _), Is.EqualTo(MissionClaimResult.NotFinished));
        Assert.That(engine.CheckClaim(Hero, "nope", SundayMorning, out _), Is.EqualTo(MissionClaimResult.Unknown));
        engine.AddProgress(Hero, MissionKind.LevelUp, 2, null, SundayMorning);
        Assert.That(engine.CheckClaim(Hero, "lvl", SundayMorning, out _), Is.EqualTo(MissionClaimResult.Ok));

        Assert.That(engine.MarkClaimed(Hero, "lvl", SundayMorning, out var reference), Is.True);
        Assert.That(reference, Is.EqualTo("mission:char-1:2026-11-15:lvl"));
        Assert.That(engine.MarkClaimed(Hero, "lvl", SundayMorning, out _), Is.False);
        Assert.That(engine.CheckClaim(Hero, "lvl", SundayMorning, out _), Is.EqualTo(MissionClaimResult.AlreadyClaimed));

        engine.Unclaim(Hero, "lvl", SundayMorning);
        Assert.That(engine.CheckClaim(Hero, "lvl", SundayMorning, out _), Is.EqualTo(MissionClaimResult.Ok), "A reward which couldn't be given stays claimable.");

        var tomorrow = SundayMorning.AddDays(1);
        engine.AddProgress(Hero, MissionKind.LevelUp, 2, null, tomorrow);
        Assert.That(engine.MarkClaimed(Hero, "lvl", tomorrow, out var next), Is.True);
        Assert.That(next, Is.Not.EqualTo(reference));
    }

    /// <summary>
    /// Finishing the daily missions counts a streak day once per day; the bonus day pays a bonus; a missed day resets it.
    /// </summary>
    [Test]
    public void StreakCountsDaysPaysTheBonusAndResetsAfterAMissedDay()
    {
        var engine = Engine(new MissionDefinition { Id = "a", Kind = MissionKind.LevelUp, Target = 1 }, new MissionDefinition { Id = "b", Kind = MissionKind.ChaosMachineMix, Target = 1 });
        engine.Configuration.StreakBonusDay = 3;
        engine.Configuration.StreakBonus = new MissionReward { Coins = 50 };

        void FinishAll(DateTime at)
        {
            engine.AddProgress(Hero, MissionKind.LevelUp, 1, null, at);
            engine.AddProgress(Hero, MissionKind.ChaosMachineMix, 1, null, at);
        }

        engine.AddProgress(Hero, MissionKind.LevelUp, 1, null, SundayMorning);
        Assert.That(engine.GetStreak(Hero, SundayMorning).Days, Is.EqualTo(0), "One of two daily missions isn't a day.");
        engine.AddProgress(Hero, MissionKind.ChaosMachineMix, 1, null, SundayMorning);
        Assert.That(engine.GetStreak(Hero, SundayMorning), Is.EqualTo((1, true, false)));

        FinishAll(SundayMorning.AddDays(1));
        Assert.That(engine.GetStreak(Hero, SundayMorning.AddDays(1)).Days, Is.EqualTo(2));
        Assert.That(engine.GetStreak(Hero, SundayMorning.AddDays(2)), Is.EqualTo((2, false, false)), "The next day shows the streak until the day is over.");
        FinishAll(SundayMorning.AddDays(2));
        Assert.That(engine.GetStreak(Hero, SundayMorning.AddDays(2)), Is.EqualTo((3, true, true)), "Day 3 is the bonus day.");
        Assert.That(engine.TakeStreakBonus(Hero, out var bonus), Is.True);
        Assert.That(bonus, Is.EqualTo("streak:char-1:2026-11-17"));
        Assert.That(engine.TakeStreakBonus(Hero, out _), Is.False, "The bonus is taken once.");

        Assert.That(engine.GetStreak(Hero, SundayMorning.AddDays(4)).Days, Is.EqualTo(0), "A missed day shows 0.");
        FinishAll(SundayMorning.AddDays(4));
        Assert.That(engine.GetStreak(Hero, SundayMorning.AddDays(4)).Days, Is.EqualTo(1), "...and starts again at 1.");
    }

    /// <summary>
    /// "At least N" daily missions finish a streak day.
    /// </summary>
    [Test]
    public void StreakNeedsOnlyTheConfiguredNumberOfMissions()
    {
        var engine = Engine(new MissionDefinition { Id = "a", Kind = MissionKind.LevelUp, Target = 1 }, new MissionDefinition { Id = "b", Kind = MissionKind.ChaosMachineMix, Target = 1 });
        engine.Configuration.StreakMissionsRequired = 1;
        engine.AddProgress(Hero, MissionKind.LevelUp, 1, null, SundayMorning);
        Assert.That(engine.GetStreak(Hero, SundayMorning).Days, Is.EqualTo(1));
    }

    /// <summary>
    /// Online time pays per full hour, auto play counts half, under the source's cap and the overall daily cap; a new day starts over.
    /// </summary>
    [Test]
    public void OnlineTimePaysFullHoursUnderTheCaps()
    {
        var engine = Engine();
        engine.Configuration.OnlineTime = new OnlineTimeCoinsConfiguration { CoinsPerHour = 5, DailyCap = 12, AutoPlayCountsHalf = true };
        var at = SundayMorning;
        Assert.That(engine.AddOnlineTime(Login, 3599, false, at), Is.Null);
        var first = engine.AddOnlineTime(Login, 1, false, at);
        Assert.That(first?.Amount, Is.EqualTo(5));
        Assert.That(first!.CoinType, Is.EqualTo(CashShopCoinType.GoblinPoints));
        Assert.That(first.Reference, Is.EqualTo("online:test8:2026-11-15:1"));

        Assert.That(engine.AddOnlineTime(Login, 3600, true, at), Is.Null, "An hour of auto play counts half an hour.");
        Assert.That(engine.AddOnlineTime(Login, 3600, true, at)?.Amount, Is.EqualTo(5));
        Assert.That(engine.AddOnlineTime(Login, 3600, false, at)?.Amount, Is.EqualTo(2), "Capped at 12 a day.");
        Assert.That(engine.AddOnlineTime(Login, 7200, false, at), Is.Null);
        Assert.That(engine.GetEarnedToday(Login, at).Online, Is.EqualTo(12));

        var tomorrow = at.AddDays(1);
        Assert.That(engine.GetEarnedToday(Login, tomorrow).Online, Is.EqualTo(0));
        Assert.That(engine.AddOnlineTime(Login, 3600, false, tomorrow)?.Amount, Is.EqualTo(5));
    }

    /// <summary>
    /// The overall daily cap: one-time rewards which don't fit wait for a later day, and are given then once.
    /// </summary>
    [Test]
    public void EarnedCoinsAreCappedPerDayAndTheRestWaits()
    {
        var engine = Engine();
        engine.Configuration.EarnedDailyCap = 100;
        var first = new CoinGrant(Login, CashShopCoinType.GoblinPoints, 80, "Level 100", "milestone:x:level:100");
        var second = new CoinGrant(Login, CashShopCoinType.GoblinPoints, 50, "Level 200", "milestone:x:level:200");
        Assert.That(engine.ReserveOrOwe(first, SundayMorning), Is.True);
        Assert.That(engine.ReserveOrOwe(second, SundayMorning), Is.False, "80 + 50 is over the cap of 100.");
        Assert.That(engine.ReserveOrOwe(second, SundayMorning), Is.False);
        Assert.That(engine.GetEarnedToday(Login, SundayMorning), Is.EqualTo((80, 0, 0, 50)), "It waits once, not twice.");
        Assert.That(engine.TakeOwed(Login, SundayMorning), Is.Empty);

        engine.Configuration.OnlineTime = new OnlineTimeCoinsConfiguration { CoinsPerHour = 30, DailyCap = 100 };
        Assert.That(engine.AddOnlineTime(Login, 3600, false, SundayMorning)?.Amount, Is.EqualTo(20), "Online time fills the rest of the cap.");

        var tomorrow = SundayMorning.AddDays(1);
        var owed = engine.TakeOwed(Login, tomorrow);
        Assert.That(owed.Select(g => g.Reference), Is.EqualTo(new[] { "milestone:x:level:200" }));
        Assert.That(engine.TakeOwed(Login, tomorrow), Is.Empty);
        Assert.That(engine.GetEarnedToday(Login, tomorrow).Earned, Is.EqualTo(50));

        var big = new CoinGrant("other", CashShopCoinType.GoblinPoints, 500, "Big", "big");
        Assert.That(engine.ReserveOrOwe(big, tomorrow), Is.True, "A reward bigger than the cap is given as the first of a day, so it isn't stuck.");
    }

    /// <summary>
    /// The login calendar: one claim per day, the n-th claim pays the n-th day's coins, a new month starts over.
    /// </summary>
    [Test]
    public void LoginCalendarClaimsOncePerDayAndStartsOverEachMonth()
    {
        var engine = Engine();
        var at = new DateTime(2026, 11, 1, 1, 0, 0, DateTimeKind.Utc);
        for (var day = 1; day <= 7; day++)
        {
            Assert.That(engine.ClaimCalendar(Login, at, out var grant), Is.EqualTo(MissionClaimResult.Ok));
            Assert.That(engine.ClaimCalendar(Login, at.AddHours(5), out _), Is.EqualTo(MissionClaimResult.AlreadyClaimed));
            Assert.That(grant!.Amount, Is.EqualTo(day == 7 ? 20 : 5), "Day 7 pays more.");
            Assert.That(grant.Reference, Is.EqualTo($"calendar:test8:2026-11:{day}"));
            at = at.AddDays(2);
        }

        Assert.That(engine.GetCalendar(Login, at), Is.EqualTo((7, false)), "Days are counted claims, not dates.");
        var december = new DateTime(2026, 12, 2, 1, 0, 0, DateTimeKind.Utc);
        Assert.That(engine.ClaimCalendar(Login, december, out var first), Is.EqualTo(MissionClaimResult.Ok));
        Assert.That(first!.Reference, Is.EqualTo("calendar:test8:2026-12:1"), "A new month starts over.");
        Assert.That(engine.GetCalendar(Login, december), Is.EqualTo((1, true)));
    }

    /// <summary>
    /// Level milestones pay once per character; resets pay from the first one after the character was first seen.
    /// </summary>
    [Test]
    public void MilestonesPayOncePerCharacter()
    {
        var engine = Engine();
        var first = engine.TakeMilestones(Hero, Login, "Hero", 210, 2);
        Assert.That(first.Select(g => g.Amount), Is.EqualTo(new[] { 20, 30 }), "Levels 100 and 200; the 2 resets before don't pay.");
        Assert.That(engine.TakeMilestones(Hero, Login, "Hero", 250, 2), Is.Empty);
        Assert.That(engine.TakeMilestones(Hero, Login, "Hero", 10, 4).Select(g => g.Reference), Is.EqualTo(new[] { "milestone:char-1:reset:3", "milestone:char-1:reset:4" }));
        Assert.That(engine.TakeMilestones("char-2", Login, "Other", 120, 0).Single().Amount, Is.EqualTo(20), "Per character.");
    }

    /// <summary>
    /// An invite is entered once; the inviter (and the invited account) get coins once, when a character of the invited
    /// account reaches the level; the same network address gives nothing; loopback addresses (the web relay) don't count.
    /// </summary>
    [Test]
    public void InviteRewardsTheInviterOnceAtTheRequiredLevel()
    {
        var engine = Engine();
        engine.SetAddress("friend", "10.0.0.5");
        engine.SetAddress("me", "10.0.0.9");
        Assert.That(engine.RegisterInvite("me", "me", null, 1), Is.EqualTo(InviteResult.Self));
        Assert.That(engine.RegisterInvite("me", null, null, 1), Is.EqualTo(InviteResult.NotFound));
        Assert.That(engine.RegisterInvite("me", "friend", null, 160), Is.EqualTo(InviteResult.TooLate));
        Assert.That(engine.RegisterInvite("me", "friend", null, 20), Is.EqualTo(InviteResult.Ok));
        Assert.That(engine.RegisterInvite("me", "other", null, 20), Is.EqualTo(InviteResult.AlreadyInvited));

        Assert.That(engine.TakeInviteRewards("me", 149), Is.Empty);
        var rewards = engine.TakeInviteRewards("me", 150);
        Assert.That(rewards.Select(g => (g.LoginName, g.Amount)), Is.EqualTo(new[] { ("friend", 100), ("me", 30) }));
        Assert.That(engine.TakeInviteRewards("me", 200), Is.Empty, "Once.");

        engine.SetAddress("twin", "10.0.0.5");
        Assert.That(engine.RegisterInvite("twin", "friend", "10.0.0.5", 1), Is.EqualTo(InviteResult.SameAddress));
        engine.SetAddress("web1", "127.0.0.1");
        engine.SetAddress("web2", "127.0.0.1");
        Assert.That(engine.RegisterInvite("web2", "web1", "127.0.0.1", 1), Is.EqualTo(InviteResult.Ok), "The relay's address is no evidence.");

        var code = engine.InviteCodeOf("friend");
        Assert.That(code, Has.Length.EqualTo(6));
        Assert.That(engine.InviteCodeOf("friend"), Is.EqualTo(code));
        Assert.That(engine.Store.FindByInviteCode(code.ToLowerInvariant()), Is.EqualTo("friend"));
    }

    /// <summary>
    /// The progress is kept in the file over a restart.
    /// </summary>
    [Test]
    public void ProgressOutlastsARestart()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hnmo-missions-{Guid.NewGuid():N}.json");
        try
        {
            var mission = new MissionDefinition { Id = "kill", Kind = MissionKind.KillMonsters, Target = 10 };
            var configuration = new MissionsConfiguration { Missions = [mission] };
            var engine = new MissionEngine(configuration, new MissionStore(path));
            engine.AddProgress(Hero, MissionKind.KillMonsters, 4, null, SundayMorning);
            engine.ClaimCalendar(Login, SundayMorning, out _);
            lock (engine.Store.Lock)
            {
                engine.Store.SaveIfChanged();
            }

            var restarted = new MissionEngine(configuration, new MissionStore(path));
            Assert.That(restarted.GetProgress(Hero, mission, SundayMorning).Value, Is.EqualTo(4));
            Assert.That(restarted.ClaimCalendar("TEST8", SundayMorning, out _), Is.EqualTo(MissionClaimResult.AlreadyClaimed), "Login names are case insensitive.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// The board and the public list are ASCII JSON with the missions, their progress and the Vietnamese titles.
    /// </summary>
    [Test]
    public void BoardIsAsciiJson()
    {
        var engine = new MissionEngine(new MissionsConfiguration(), new MissionStore());
        engine.AddProgress(Hero, MissionKind.KillMonsters, 12, null, SundayMorning);
        var json = MissionBoardWriter.WriteBoard(engine, Hero, Login, SundayMorning, new MissionBoardEvent("finished", "Ok", ["daily-kill"]));
        Assert.That(json.All(c => c < 128), Is.True);
        using var document = JsonDocument.Parse(json);
        var missions = document.RootElement.GetProperty("missions");
        Assert.That(missions.GetArrayLength(), Is.EqualTo(9));
        var kill = missions.EnumerateArray().First(m => m.GetProperty("id").GetString() == "daily-kill");
        Assert.That(kill.GetProperty("value").GetInt32(), Is.EqualTo(12));
        Assert.That(kill.GetProperty("titleVi").GetString(), Is.EqualTo("Hạ gục 300 quái vật"));
        Assert.That(document.RootElement.GetProperty("calendar").GetProperty("rewards").GetArrayLength(), Is.EqualTo(30));
        Assert.That(document.RootElement.GetProperty("event").GetProperty("type").GetString(), Is.EqualTo("finished"));

        var publicJson = MissionBoardWriter.WritePublic(new MissionsConfiguration(), SundayMorning, (g, n) => g == 14 && n == 13 ? "Jewel of Bless" : null);
        using var publicDocument = JsonDocument.Parse(publicJson);
        Assert.That(publicDocument.RootElement.GetProperty("missions").GetArrayLength(), Is.EqualTo(9));
        Assert.That(publicJson, Does.Contain("Jewel of Bless"));
        Assert.That(publicJson, Does.Not.Contain("test8"));
    }

    /// <summary>
    /// The configuration survives the JSON round trip of the plug-in configuration (enums as text).
    /// </summary>
    [Test]
    public void ConfigurationRoundTrips()
    {
        var json = JsonSerializer.Serialize(new MissionsConfiguration());
        var back = JsonSerializer.Deserialize<MissionsConfiguration>(json)!;
        Assert.That(back.Missions, Has.Count.EqualTo(9));
        Assert.That(back.Missions.First(m => m.Id == "weekly-win").Kind, Is.EqualTo(MissionKind.WinEvent));
        Assert.That(back.LoginCalendar.CoinsOf(28), Is.EqualTo(60));
        Assert.That(json, Does.Contain("\"KillMonsters\""));
    }

    /// <summary>
    /// The admin panel's text forms of reward items and of level/day coins.
    /// </summary>
    [Test]
    public void ConfigurationTextsParseAndFormat()
    {
        Assert.That(MissionConfigurationText.TryParseItems("14 13 0 1; 12 15", out var items), Is.True);
        Assert.That(items.Select(i => (i.Group, i.Number, i.Level, i.Count)), Is.EqualTo(new[] { ((short)14, (short)13, (byte)0, 1), ((short)12, (short)15, (byte)0, 1) }));
        Assert.That(MissionConfigurationText.FormatItems(items), Is.EqualTo("14 13 0 1; 12 15 0 1"));
        Assert.That(MissionConfigurationText.TryParseItems(string.Empty, out var none) && none.Count == 0, Is.True);
        Assert.That(MissionConfigurationText.TryParseItems("14", out _), Is.False);
        Assert.That(MissionConfigurationText.TryParseItems("16 1 0 1", out _), Is.False);

        Assert.That(MissionConfigurationText.TryParsePairs("200:30, 100:20", 1000, out var pairs), Is.True);
        Assert.That(MissionConfigurationText.FormatPairs(pairs), Is.EqualTo("100:20, 200:30"));
        Assert.That(MissionConfigurationText.TryParsePairs("32:5", 31, out _), Is.False);
        Assert.That(MissionConfigurationText.TryParsePairs("7-5", 31, out _), Is.False);
    }

    private static MissionEngine Engine(params MissionDefinition[] missions)
    {
        return new MissionEngine(new MissionsConfiguration { Missions = missions.ToList() }, new MissionStore());
    }
}
