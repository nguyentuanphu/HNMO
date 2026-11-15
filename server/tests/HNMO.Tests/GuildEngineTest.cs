// <copyright file="GuildEngineTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Text.Json;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns.Guilds;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// Tests for HNMO's guild levels: guild experience with the daily cap per member (Sydney days), the levels and their
/// buffs, the weekly ladder (Monday 00:00 Sydney time, with daylight saving) which is paid once, the castle siege bonus
/// and the rewards which wait for room in the bag.
/// </summary>
[TestFixture]
public class GuildEngineTest
{
    private static readonly Guid Alpha = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Beta = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Gamma = new("33333333-3333-3333-3333-333333333333");

    /// <summary>
    /// Experience from the sources raises the level at the thresholds; a source that's off gives nothing.
    /// </summary>
    [Test]
    public void ExperienceRaisesTheLevelAtTheThresholds()
    {
        var configuration = new GuildsConfiguration { DailyCapPerMember = 0 };
        var engine = new GuildEngine(configuration, new GuildStore());
        var now = Utc(2026, 11, 16, 2, 0);
        Assert.That(engine.XpFor(GuildXpSource.MonsterKill), Is.EqualTo(1));
        Assert.That(engine.XpFor(GuildXpSource.WorldBossDamage, 12.5), Is.EqualTo(25), "2 per percent of the damage.");

        var result = engine.AddXp(Alpha, "Alpha", "c1", "Ann", GuildXpSource.Mission, 1999, now);
        Assert.That(result.Granted, Is.EqualTo(1999));
        Assert.That(result.LeveledUp, Is.False);
        Assert.That(engine.Progress(Alpha, now)!.Level, Is.EqualTo(1));

        result = engine.AddXp(Alpha, "Alpha", "c1", "Ann", GuildXpSource.Mission, 1, now);
        Assert.That(result, Is.EqualTo(new GuildXpResult(1, 1, 2)), "2000 is level 2.");
        result = engine.AddXp(Alpha, "Alpha", "c2", "Bob", GuildXpSource.Mission, 30_000, now);
        Assert.That(result.NewLevel, Is.EqualTo(5), "32,000 is past 25,000 (level 5) but not 45,000.");
        Assert.That(engine.LevelOf(10_000_000), Is.EqualTo(10), "The last level is the top.");

        configuration.MonsterKill.Enabled = false;
        Assert.That(engine.XpFor(GuildXpSource.MonsterKill), Is.EqualTo(0));
        var progress = engine.Progress(Alpha, now)!;
        Assert.That(progress.WeekMembers["Ann"], Is.EqualTo(2000));
        Assert.That(progress.WeekMembers["Bob"], Is.EqualTo(30_000));
    }

    /// <summary>
    /// A member's daily cap stops their experience for the day (Sydney day, midnight); the castle siege isn't capped.
    /// </summary>
    [Test]
    public void TheDailyCapPerMemberResetsAtSydneyMidnight()
    {
        var engine = new GuildEngine(new GuildsConfiguration { DailyCapPerMember = 100 }, new GuildStore());

        // 16 November 2026, 12:59 UTC = 23:59 in Sydney (summer time, UTC+11).
        var lateEvening = Utc(2026, 11, 16, 12, 59);
        Assert.That(engine.AddXp(Alpha, "Alpha", "c1", "Ann", GuildXpSource.MonsterKill, 70, lateEvening).Granted, Is.EqualTo(70));
        Assert.That(engine.AddXp(Alpha, "Alpha", "c1", "Ann", GuildXpSource.MonsterKill, 70, lateEvening).Granted, Is.EqualTo(30), "Capped at 100.");
        Assert.That(engine.AddXp(Alpha, "Alpha", "c1", "Ann", GuildXpSource.OnlineTime, 5, lateEvening).Granted, Is.EqualTo(0));
        Assert.That(engine.AddXp(Alpha, "Alpha", "c2", "Bob", GuildXpSource.MonsterKill, 50, lateEvening).Granted, Is.EqualTo(50), "Each member has a cap.");
        Assert.That(engine.AddXp(Alpha, "Alpha", null, null, GuildXpSource.CastleSiege, 3000, lateEvening).Granted, Is.EqualTo(3000), "The siege isn't capped.");
        Assert.That(engine.Today("c1", lateEvening), Is.EqualTo(100));

        var midnight = Utc(2026, 11, 16, 13, 0);
        Assert.That(engine.Today("c1", midnight), Is.EqualTo(0), "00:00 in Sydney starts a new day.");
        Assert.That(engine.AddXp(Alpha, "Alpha", "c1", "Ann", GuildXpSource.MonsterKill, 70, midnight).Granted, Is.EqualTo(70));
        Assert.That(engine.Progress(Alpha, midnight)!.Xp, Is.EqualTo(100 + 50 + 3000 + 70));
    }

    /// <summary>
    /// The buff is the level's own bonuses: bonus experience, a zen drop multiplier and final defense; none when the
    /// buffs are off or the level gives nothing.
    /// </summary>
    [Test]
    public void TheBuffComesFromTheLevel()
    {
        var configuration = new GuildsConfiguration();
        var engine = new GuildEngine(configuration, new GuildStore());
        Assert.That(engine.BuffOf(1), Is.Null, "Level 1 gives no buff.");
        var buff = engine.BuffOf(5)!;
        Assert.That((buff.ExperiencePercent, buff.ZenPercent, buff.Defense), Is.EqualTo((3d, 5d, 10)));

        var elements = GuildsPlugIn.BuffElements(buff);
        Assert.That(elements.Select(e => e.Target), Is.EqualTo(new[] { Stats.BonusExperienceRate, Stats.MoneyAmountRate, Stats.DefenseFinal }));
        Assert.That(elements[0].Element.Value, Is.EqualTo(0.03f).Within(0.0001f));
        Assert.That(elements[0].Element.AggregateType, Is.EqualTo(AggregateType.AddRaw));
        Assert.That(elements[1].Element.Value, Is.EqualTo(1.05f).Within(0.0001f));
        Assert.That(elements[1].Element.AggregateType, Is.EqualTo(AggregateType.Multiplicate));
        Assert.That(elements[2].Element.Value, Is.EqualTo(10f));
        Assert.That(elements[2].Element.AggregateType, Is.EqualTo(AggregateType.AddFinal));
        Assert.That(GuildsPlugIn.BuffElements(engine.Level(2)!), Has.Length.EqualTo(1), "Level 2 gives only experience.");

        configuration.BuffsEnabled = false;
        Assert.That(engine.BuffOf(5), Is.Null);
    }

    /// <summary>
    /// The week ends on Monday 00:00 in Sydney: in summer time that is Sunday 13:00 UTC, in winter Sunday 14:00 UTC, and
    /// on the week when the clocks go back (Sunday 5 April 2026) the Monday after it starts at 14:00 UTC.
    /// </summary>
    [Test]
    public void TheWeekRollsOverOnMondayInSydney()
    {
        var engine = new GuildEngine(new GuildsConfiguration { DailyCapPerMember = 0 }, new GuildStore());

        // Summer: Monday 23 November 2026 00:00 AEDT = Sunday 22 November 13:00 UTC.
        engine.AddXp(Alpha, "Alpha", "c1", "Ann", GuildXpSource.Mission, 500, Utc(2026, 11, 22, 12, 59));
        Assert.That(engine.Ladder(Utc(2026, 11, 22, 12, 59)), Has.Count.EqualTo(1));
        Assert.That(engine.Ladder(Utc(2026, 11, 22, 13, 0)), Is.Empty, "A new week.");
        Assert.That(engine.LastWeek(Utc(2026, 11, 22, 13, 0))!.Week, Is.EqualTo("2026-11-16"));
        Assert.That(engine.Progress(Alpha, Utc(2026, 11, 22, 13, 0))!.Xp, Is.EqualTo(500), "The level's experience stays.");

        // The clocks go back on Sunday 5 April 2026 (03:00 AEDT -> 02:00 AEST): Monday 6 April 00:00 AEST = 5 April 14:00 UTC.
        var april = new GuildEngine(new GuildsConfiguration { DailyCapPerMember = 0 }, new GuildStore());
        april.AddXp(Beta, "Beta", "c2", "Bob", GuildXpSource.Mission, 200, Utc(2026, 4, 5, 13, 30));
        Assert.That(april.Ladder(Utc(2026, 4, 5, 13, 59)), Has.Count.EqualTo(1), "13:59 UTC is still Sunday 23:59 AEST.");
        Assert.That(april.Ladder(Utc(2026, 4, 5, 14, 0)), Is.Empty);
        Assert.That(april.LastWeek(Utc(2026, 4, 5, 14, 0))!.Week, Is.EqualTo("2026-03-30"));

        // The clocks go forward on Sunday 4 October 2026: Monday 5 October 00:00 AEDT = 4 October 13:00 UTC.
        var october = new GuildEngine(new GuildsConfiguration { DailyCapPerMember = 0 }, new GuildStore());
        october.AddXp(Beta, "Beta", "c2", "Bob", GuildXpSource.Mission, 200, Utc(2026, 10, 4, 12, 0));
        Assert.That(october.Ladder(Utc(2026, 10, 4, 12, 59)), Has.Count.EqualTo(1));
        Assert.That(october.Ladder(Utc(2026, 10, 4, 13, 0)), Is.Empty);
    }

    /// <summary>
    /// The finished week's ladder is ranked by points (a tie goes to the guild there first), the rewards go by rank
    /// for guilds with enough points, and a week is paid once: its references are unique per week, guild and member.
    /// </summary>
    [Test]
    public void TheLadderIsPaidOnceWithUniqueReferences()
    {
        var configuration = new GuildsConfiguration { DailyCapPerMember = 0, LadderMinimumXp = 100 };
        var engine = new GuildEngine(configuration, new GuildStore());
        var monday = Utc(2026, 11, 16, 1, 0);
        engine.AddXp(Beta, "Beta", "c2", "Bob", GuildXpSource.Mission, 300, monday);
        engine.AddXp(Alpha, "Alpha", "c1", "Ann", GuildXpSource.Mission, 300, monday.AddMinutes(1));
        engine.AddXp(Gamma, "Gamma", "c3", "Cy", GuildXpSource.Mission, 50, monday);
        engine.RecordCastleSiege(Gamma, "Gamma", monday.AddHours(2));
        Assert.That(engine.Sieges().Single().Name, Is.EqualTo("Gamma"));

        var next = Utc(2026, 11, 22, 13, 0);
        var weeks = engine.TakeUnpaidWeeks(next);
        Assert.That(weeks, Has.Count.EqualTo(1));
        var lines = weeks[0].Lines;
        Assert.That(lines.Select(l => l.Name), Is.EqualTo(new[] { "Gamma", "Beta", "Alpha" }), "The siege's 3000 XP and 5000 ladder bonus put Gamma first; Beta got its 300 first.");
        Assert.That(lines[0].Xp, Is.EqualTo(50 + 3000 + 5000));
        Assert.That(engine.Progress(Gamma, next)!.Xp, Is.EqualTo(3050), "The ladder bonus doesn't raise the level.");

        var first = engine.RewardOf(1, lines[0].Xp)!;
        Assert.That(first.CoinsPerMember, Is.EqualTo(150));
        Assert.That(engine.RewardOf(4, 500)!.RankFrom, Is.EqualTo(4), "Ranks 4 to 5 share a reward.");
        Assert.That(engine.RewardOf(6, 500), Is.Null);
        Assert.That(engine.RewardOf(1, 99), Is.Null, "Too few points.");

        var member = Guid.NewGuid();
        var grant = GuildEngine.LadderGrant(weeks[0].Week, lines[0], first, member, "Cy", "cy-login", isMaster: true)!;
        Assert.That(grant.Amount, Is.EqualTo(250), "The master gets the bonus.");
        Assert.That(grant.Reference, Is.EqualTo(GuildEngine.LadderReference("2026-11-16", Gamma, member)));
        Assert.That(GuildEngine.LadderGrant(weeks[0].Week, lines[0], first, Guid.NewGuid(), "Dee", "dee", false)!.Reference, Is.Not.EqualTo(grant.Reference));
        Assert.That(engine.NotePaid(weeks[0], grant.Reference), Is.True);
        Assert.That(engine.NotePaid(weeks[0], grant.Reference), Is.False, "A retry pays nobody twice.");

        engine.MarkPaid(weeks[0]);
        Assert.That(engine.TakeUnpaidWeeks(next), Is.Empty);
        Assert.That(engine.TakeUnpaidWeeks(next.AddDays(1)), Is.Empty, "Still once.");
        Assert.That(engine.LastWeek(next)!.Week, Is.EqualTo("2026-11-16"));

        configuration.LadderEnabled = false;
        Assert.That(engine.RewardOf(1, 10_000), Is.Null);
    }

    /// <summary>
    /// Rewards waiting for room in the bag are kept once per reference; taken ones can be given back.
    /// </summary>
    [Test]
    public void WaitingRewardsCountOnce()
    {
        var engine = new GuildEngine(new GuildsConfiguration(), new GuildStore());
        var reward = new GuildOwedReward { Reference = "r1", Week = "2026-11-16", Rank = 1, Reward = new MissionReward { Zen = 100 } };
        Assert.That(engine.AddOwed("c1", reward), Is.True);
        Assert.That(engine.AddOwed("c1", reward), Is.False);
        var taken = engine.TakeOwed("c1");
        Assert.That(taken, Has.Count.EqualTo(1));
        Assert.That(engine.Owed("c1"), Is.Empty);
        engine.ReturnOwed("c1", taken);
        Assert.That(engine.Owed("c1"), Has.Count.EqualTo(1));
    }

    /// <summary>
    /// The store keeps the progress in its file over a restart.
    /// </summary>
    [Test]
    public void TheStoreOutlastsARestart()
    {
        var file = Path.Combine(Path.GetTempPath(), $"hnmo-guilds-{Guid.NewGuid():N}.json");
        try
        {
            var store = new GuildStore(file);
            var engine = new GuildEngine(new GuildsConfiguration(), store);
            engine.AddXp(Alpha, "Alpha", "c1", "Ann", GuildXpSource.Mission, 15, Utc(2026, 11, 16, 1, 0));
            lock (store.Lock)
            {
                store.SaveIfChanged();
            }

            var again = new GuildEngine(new GuildsConfiguration(), new GuildStore(file));
            var progress = again.Progress(Alpha, Utc(2026, 11, 16, 2, 0))!;
            Assert.That((progress.Name, progress.Xp, progress.WeekXp), Is.EqualTo(("Alpha", 15L, 15L)));
            Assert.That(again.Today("c1", Utc(2026, 11, 16, 2, 0)), Is.EqualTo(15));
        }
        finally
        {
            File.Delete(file);
        }
    }

    /// <summary>
    /// The client's state names the guild, its level, the experience to the next level, the rank and the buff, in plain
    /// ASCII; the website's state leaves out disbanded guilds and has the member counts.
    /// </summary>
    [Test]
    public void TheStatesAreWritten()
    {
        var engine = new GuildEngine(new GuildsConfiguration { DailyCapPerMember = 0, BuffNameVi = "Phúc lành" }, new GuildStore());
        var now = Utc(2026, 11, 16, 1, 0);
        engine.AddXp(Alpha, "Alpha", "c1", "Ann", GuildXpSource.Mission, 6500, now);
        engine.AddXp(Beta, "Beta", "c2", "Bob", GuildXpSource.Mission, 10, now);
        var json = GuildStateWriter.WriteState(engine, new GuildKey(Alpha, "Alpha"), "c1", "Ann", now, new GuildStateEvent("levelup", Level: 3));
        Assert.That(json.All(c => c < 128), Is.True, "ASCII only.");
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.That(root.GetProperty("level").GetInt32(), Is.EqualTo(3));
        Assert.That(root.GetProperty("nextXp").GetInt64(), Is.EqualTo(12_000));
        Assert.That(root.GetProperty("rank").GetInt32(), Is.EqualTo(1));
        Assert.That(root.GetProperty("myWeekXp").GetInt64(), Is.EqualTo(6500));
        Assert.That(root.GetProperty("buff").GetProperty("zen").GetDouble(), Is.EqualTo(2));
        Assert.That(root.GetProperty("buffNameVi").GetString(), Is.EqualTo("Phúc lành"));
        Assert.That(root.GetProperty("event").GetProperty("type").GetString(), Is.EqualTo("levelup"));

        var none = JsonDocument.Parse(GuildStateWriter.WriteState(engine, null, "c9", "Nobody", now, null)).RootElement;
        Assert.That(none.GetProperty("inGuild").GetBoolean(), Is.False);

        var website = JsonDocument.Parse(GuildStateWriter.WritePublic(engine, now, new Dictionary<Guid, int> { [Alpha] = 7 })).RootElement;
        Assert.That(website.GetProperty("ladder").GetArrayLength(), Is.EqualTo(1), "Beta no longer exists.");
        Assert.That(website.GetProperty("ladder")[0].GetProperty("members").GetInt32(), Is.EqualTo(7));
        Assert.That(website.GetProperty("levels").GetArrayLength(), Is.EqualTo(10));
    }

    private static DateTime Utc(int year, int month, int day, int hour, int minute) => new(year, month, day, hour, minute, 0, DateTimeKind.Utc);
}
