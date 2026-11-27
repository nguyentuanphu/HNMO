// <copyright file="AutoErrorReportsTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Globalization;
using HNMO.GameLogic.PlugIns.BugReports;

/// <summary>
/// The web client's automatic error reports (HNMO extra; owner 2026-11-27: they go to the host only): grouping by
/// fingerprint, the caps per account and per server in Sydney days, the owner's decisions, and the developer's message,
/// which never carries a login.
/// </summary>
[TestFixture]
public sealed class AutoErrorReportsTest
{
    private static readonly TimeZoneInfo Sydney = GameLogic.PlugIns.News.NewsStore.ResolveSydneyZone();

    /// <summary>Reports of one fingerprint make one group: count, distinct accounts, first and last time, versions, the last 5 samples.</summary>
    [Test]
    public void ReportsAreGroupedByFingerprint()
    {
        var data = new BugReportStoreData();
        var settings = new BugReportsConfiguration();
        var start = new DateTime(2026, 11, 27, 1, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 7; i++)
        {
            var (outcome, group) = AutoErrorEngine.Submit(data, i % 2 == 0 ? "alice" : "Bob", "x is undefined|main.js|1200", Sample($"1.0.{i % 3}"), settings, start.AddMinutes(i), Sydney);
            Assert.That(outcome, Is.EqualTo(i == 0 ? AutoErrorEngine.Outcome.NewGroup : AutoErrorEngine.Outcome.Added));
            Assert.That(group!.Id, Is.EqualTo("E1"));
        }

        AutoErrorEngine.Submit(data, "alice", "WebGL context lost|webgl|0", Sample("1.0.0", "webgl"), settings, start.AddMinutes(10), Sydney);

        var groups = AutoErrorEngine.ByCount(data);
        Assert.That(groups.Select(g => g.Id), Is.EqualTo(new[] { "E1", "E2" }), "most reports first");
        var first = groups[0];
        Assert.That(first.Count, Is.EqualTo(7));
        Assert.That(first.Accounts, Has.Count.EqualTo(2), "alice and bob (any case)");
        Assert.That(first.FirstSeenUtc, Is.EqualTo(start));
        Assert.That(first.LastSeenUtc, Is.EqualTo(start.AddMinutes(6)));
        Assert.That(first.Versions, Is.EquivalentTo(new[] { "1.0.0", "1.0.1", "1.0.2" }));
        Assert.That(first.Samples, Has.Count.EqualTo(AutoErrorEngine.MaxSamples));
        Assert.That(first.Samples[^1].ReceivedUtc, Is.EqualTo(start.AddMinutes(6)), "the newest sample is kept");
        Assert.That(groups[1].Kind, Is.EqualTo("webgl"));
    }

    /// <summary>An account and the server have caps a Sydney day; the next Sydney day starts again. Off keeps nothing.</summary>
    [Test]
    public void CapsFollowSydneyDays()
    {
        var data = new BugReportStoreData();
        var settings = new BugReportsConfiguration { AutomaticDailyLimitPerAccount = 3, AutomaticDailyLimitPerServer = 5 };

        // 2026-11-27 12:30 UTC is 23:30 in Sydney (+11); 13:05 UTC is the next Sydney day.
        var lateEvening = new DateTime(2026, 11, 27, 12, 30, 0, DateTimeKind.Utc);
        for (var i = 0; i < 3; i++)
        {
            Assert.That(AutoErrorEngine.Submit(data, "alice", $"e{i}", Sample(), settings, lateEvening, Sydney).Group, Is.Not.Null);
        }

        Assert.That(AutoErrorEngine.Submit(data, "ALICE", "e9", Sample(), settings, lateEvening, Sydney).Outcome, Is.EqualTo(AutoErrorEngine.Outcome.AccountCap));
        Assert.That(AutoErrorEngine.Submit(data, "bob", "e1", Sample(), settings, lateEvening, Sydney).Group, Is.Not.Null);
        Assert.That(AutoErrorEngine.Submit(data, "carol", "e1", Sample(), settings, lateEvening, Sydney).Group, Is.Not.Null);
        Assert.That(AutoErrorEngine.Submit(data, "dave", "e1", Sample(), settings, lateEvening, Sydney).Outcome, Is.EqualTo(AutoErrorEngine.Outcome.ServerCap));
        Assert.That(AutoErrorEngine.Find(data, "E2")!.Count, Is.EqualTo(3), "dropped reports aren't counted");

        var nextSydneyDay = new DateTime(2026, 11, 27, 13, 5, 0, DateTimeKind.Utc);
        Assert.That(AutoErrorEngine.Submit(data, "alice", "e1", Sample(), settings, nextSydneyDay, Sydney).Outcome, Is.EqualTo(AutoErrorEngine.Outcome.Added));
        Assert.That(data.AutoDay, Is.EqualTo("2026-11-28"));

        settings.AutomaticReports = false;
        Assert.That(AutoErrorEngine.Submit(data, "alice", "e1", Sample(), settings, nextSydneyDay, Sydney).Outcome, Is.EqualTo(AutoErrorEngine.Outcome.Off));
        Assert.That(AutoErrorEngine.Submit(data, "alice", " ", Sample(), new BugReportsConfiguration(), nextSydneyDay, Sydney).Outcome, Is.EqualTo(AutoErrorEngine.Outcome.Invalid));
    }

    /// <summary>A fixed error that comes back opens again; an ignored one is counted without new samples.</summary>
    [Test]
    public void FixedComesBackAndIgnoredKeepsNoSamples()
    {
        var data = new BugReportStoreData();
        var settings = new BugReportsConfiguration();
        var now = new DateTime(2026, 11, 27, 2, 0, 0, DateTimeKind.Utc);
        AutoErrorEngine.Submit(data, "alice", "fp", Sample(), settings, now, Sydney);
        Assert.That(AutoErrorEngine.SetStatus(data, "E1", AutoErrorStatus.Fixed), Is.Not.Null);
        Assert.That(AutoErrorEngine.SetStatus(data, "E9", AutoErrorStatus.Fixed), Is.Null);

        AutoErrorEngine.Submit(data, "alice", "fp", Sample(), settings, now.AddHours(1), Sydney);
        var group = AutoErrorEngine.Find(data, "e1")!;
        Assert.That(group.Status, Is.EqualTo(AutoErrorStatus.Open));
        Assert.That(group.Reopened, Is.True);

        AutoErrorEngine.SetStatus(data, "E1", AutoErrorStatus.Ignored);
        var samples = group.Samples.Count;
        AutoErrorEngine.Submit(data, "bob", "fp", Sample(), settings, now.AddHours(2), Sydney);
        Assert.That(group.Status, Is.EqualTo(AutoErrorStatus.Ignored));
        Assert.That(group.Count, Is.EqualTo(3));
        Assert.That(group.Samples, Has.Count.EqualTo(samples));
    }

    /// <summary>The developer gets the group without any login; the character only when the owner allows it.</summary>
    [Test]
    public void TheDevelopersMessageHasNoLogin()
    {
        var data = new BugReportStoreData();
        var sample = Sample();
        sample.Character = "Hero";
        AutoErrorEngine.Submit(data, "secret-login", "fp", sample, new BugReportsConfiguration(), DateTime.UtcNow, Sydney);
        var group = AutoErrorEngine.Find(data, "E1")!;
        string Text(DateTime t) => t.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        var hidden = AutoErrorEngine.HubMessage(group, shareCharacter: false, Text).ToJsonString();
        Assert.That(hidden, Does.Not.Contain("secret-login"));
        Assert.That(hidden, Does.Not.Contain("Hero"));
        Assert.That(hidden, Does.Contain("\"kind\":\"auto-error\""));
        Assert.That(hidden, Does.Contain("\"accounts\":1"));

        var shared = AutoErrorEngine.HubMessage(group, shareCharacter: true, Text).ToJsonString();
        Assert.That(shared, Does.Not.Contain("secret-login"));
        Assert.That(shared, Does.Contain("Hero"));
    }

    /// <summary>The plug-in queues a group for the weekly report with the players' reports, and marks it sent.</summary>
    [Test]
    public void AGroupGoesWithTheWeeklyReport()
    {
        var plugIn = new BugReportsPlugIn { Configuration = new BugReportsConfiguration { FileName = string.Empty } };
        Assert.That(plugIn.ForwardAuto("E1").Ok, Is.False, "there's no such error yet");
        Assert.That(plugIn.QueuedForDeveloper(), Is.Empty);
    }

    private static AutoErrorSample Sample(string version = "1.0.0", string kind = "error") => new()
    {
        Kind = kind,
        Message = "x is undefined",
        Stack = "at f (main.js:1200:5)",
        Version = version,
        Browser = "Chrome 131",
        Os = "Windows",
        Device = "1920x1080 dpr 1",
        Map = "Lorencia",
        Gpu = "ANGLE",
    };
}
