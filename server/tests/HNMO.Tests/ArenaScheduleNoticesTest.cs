// <copyright file="ArenaScheduleNoticesTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.GameLogic.PlugIns.Arena;
using HNMO.GameLogic.PlugIns.EventAlerts;
using HNMO.GameLogic.PlugIns.Timetable;
using HNMO.Persistence;

/// <summary>The Arena's battles on the home servers: the notices, the events timetable and the web push category.</summary>
[TestFixture]
public sealed class ArenaScheduleNoticesTest
{
    private static readonly ArenaScheduleEntry GuildWar = new("guildwar", "Sat, Sun", "21:00", new DateTime(2026, 11, 21, 10, 0, 0, DateTimeKind.Utc), DateTime.UtcNow);

    /// <summary>The notice names the Sydney time, the map and how to get there, in English and Vietnamese.</summary>
    [Test]
    public void TheNoticeSaysWhenAndWhere()
    {
        var (en, vi) = ArenaScheduleNoticesPlugIn.NoticeOf(GuildWar, starting: false);
        Assert.That(en, Does.Contain("Guild war at 21:00 (Sydney)").And.Contain("Valley of Loren").And.Contain("Arena Herald"));
        Assert.That(vi, Does.Contain("Bang chiến lúc 21:00"));
        var royale = GuildWar with { Kind = "royale" };
        Assert.That(ArenaScheduleNoticesPlugIn.NoticeOf(royale, starting: true).En, Does.Contain("battle royale starts now"));
    }

    /// <summary>A published battle becomes a timetable entry with its days, times and next start.</summary>
    [Test]
    public void ABattleIsATimetableEntry()
    {
        var item = TimetableSourceCollector.FromArena(GuildWar);
        Assert.That(item.Id, Is.EqualTo("arena-guildwar"));
        Assert.That(item.AllowedDays, Is.EqualTo(new[] { DayOfWeek.Saturday, DayOfWeek.Sunday }));
        Assert.That(item.DailyTimes, Is.EqualTo(new[] { new TimeOnly(21, 0) }));
        Assert.That(item.ExplicitNextStartUtc, Is.EqualTo(GuildWar.NextStartUtc));
        Assert.That(item.NameVi, Is.EqualTo("Đấu trường: bang chiến"));
    }

    /// <summary>The web push alerts file the Arena's battles under their own category, offered by default.</summary>
    [Test]
    public void WebPushHasAnArenaCategory()
    {
        var entry = new TimetableComputedEntry { Id = "arena-royale", Category = "event", NameEn = "", NameVi = "", MapsEn = "", MapsVi = "", DropsEn = "", DropsVi = "", ScheduleText = "" };
        Assert.That(EventAlertsEngine.ClassifyCategoryKey(entry), Is.EqualTo("arena"));
        Assert.That(new EventAlertsConfiguration().AvailableCategories, Does.Contain("arena"));
    }
}
