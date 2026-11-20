// <copyright file="BugReportsTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using HNMO.GameLogic.PlugIns.BugReports;

/// <summary>
/// HNMO's bug reports: the daily limit in Sydney days, the owner's decisions and answers the player sees once, and the
/// Discord message for the HNMO developer, which carries no login.
/// </summary>
[TestFixture]
public sealed class BugReportsTest
{
    private static readonly TimeZoneInfo Sydney = GameLogic.PlugIns.News.NewsStore.ResolveSydneyZone();

    /// <summary>Five reports a Sydney day, then a refusal; the next Sydney day starts again. Short texts are refused.</summary>
    [Test]
    public void TheDailyLimitFollowsSydneyDays()
    {
        var data = new BugReportStoreData();
        var settings = new BugReportsConfiguration();

        // 2026-11-20 13:00 UTC is 2026-11-21 00:00 in Sydney (daylight saving time, +11).
        var lateEvening = new DateTime(2026, 11, 20, 12, 30, 0, DateTimeKind.Utc);
        Assert.That(BugReportEngine.Submit(data, Report("too short"), settings, lateEvening, Sydney).Report, Is.Null);
        for (var i = 0; i < 5; i++)
        {
            Assert.That(BugReportEngine.Submit(data, Report($"The sword glows wrong {i}"), settings, lateEvening, Sydney).Report, Is.Not.Null);
        }

        var refused = BugReportEngine.Submit(data, Report("One report too many today"), settings, lateEvening, Sydney);
        Assert.That(refused.Report, Is.Null);
        Assert.That(refused.Vi, Does.Contain("Hôm nay"));
        Assert.That(BugReportEngine.LeftToday(data, "alice", settings, lateEvening, Sydney), Is.Zero);

        var afterMidnightInSydney = new DateTime(2026, 11, 20, 13, 5, 0, DateTimeKind.Utc);
        Assert.That(BugReportEngine.LeftToday(data, "alice", settings, afterMidnightInSydney, Sydney), Is.EqualTo(5));
        var next = BugReportEngine.Submit(data, Report("A new Sydney day has started", "weird"), settings, afterMidnightInSydney, Sydney).Report;
        Assert.That(next!.Id, Is.EqualTo("B6"));
        Assert.That(next.Category, Is.EqualTo("other"), "an unknown category becomes other");
        Assert.That(BugReportEngine.LeftToday(data, "bob", settings, lateEvening, Sydney), Is.EqualTo(5), "per account");
    }

    /// <summary>The owner's status and answer reach the player once; a duplicate needs an existing other report.</summary>
    [Test]
    public void DecisionsAndAnswersAreSeenOnce()
    {
        var data = new BugReportStoreData();
        var now = DateTime.UtcNow;
        var first = BugReportEngine.Submit(data, Report("The map is black in Devias"), new BugReportsConfiguration(), now, Sydney).Report!;
        var second = BugReportEngine.Submit(data, Report("Devias shows nothing at all"), new BugReportsConfiguration(), now, Sydney).Report!;
        Assert.That(BugReportEngine.TakeUnseen(data, "alice"), Is.Empty);

        Assert.That(BugReportEngine.SetStatus(data, second.Id, BugReportStatus.Duplicate, "B99"), Is.Null);
        Assert.That(BugReportEngine.SetStatus(data, second.Id, BugReportStatus.Duplicate, second.Id), Is.Null);
        Assert.That(BugReportEngine.SetStatus(data, second.Id, BugReportStatus.Duplicate, "b1")!.DuplicateOf, Is.EqualTo(first.Id));
        BugReportEngine.SetStatus(data, first.Id, BugReportStatus.Confirmed);
        BugReportEngine.Reply(data, first.Id, " Thanks, we're on it. ", now);

        var unseen = BugReportEngine.TakeUnseen(data, "ALICE");
        Assert.That(unseen.Select(r => r.Id), Is.EquivalentTo(new[] { "B1", "B2" }));
        Assert.That(unseen.Single(r => r.Id == "B1").Reply, Is.EqualTo("Thanks, we're on it."));
        Assert.That(BugReportEngine.TakeUnseen(data, "alice"), Is.Empty, "seen once");
        Assert.That(BugReportEngine.Mine(data, "alice").First().Id, Is.EqualTo("B2"), "newest first");
    }

    /// <summary>The developer's message names the server's license, never the login; the character only when allowed.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task TheDeveloperGetsNoLoginAsync()
    {
        var report = Report("Skills show no effect");
        report.Id = "B7";
        report.Character = "HeroDk";
        report.Info = "version 1.2\nbrowser Chrome";
        var message = BugReportForwarder.BuildMessage(report, "Friends server", shareCharacter: false).ToJsonString();
        Assert.That(message, Does.Contain("Friends server").And.Contain("Skills show no effect"));
        Assert.That(message, Does.Not.Contain("alice").And.Not.Contain("HeroDk"));
        Assert.That(BugReportForwarder.BuildMessage(report, null, shareCharacter: true).ToJsonString(), Does.Contain("HeroDk").And.Contain("unlicensed"));

        Assert.That(BugReportForwarder.IsDiscordWebhook("https://discord.com/api/webhooks/1/abc"), Is.True);
        Assert.That(BugReportForwarder.IsDiscordWebhook("http://discord.com/api/webhooks/1/abc"), Is.False);
        Assert.That(BugReportForwarder.IsDiscordWebhook("https://example.com/api/webhooks/1/abc"), Is.False);

        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var (ok, error) = await BugReportForwarder.SendAsync("https://discord.com/api/webhooks/1/abc", report, "Friends server", false, client).ConfigureAwait(false);
        Assert.That(ok, Is.True, error);
        Assert.That(handler.Body, Is.Not.Null);
        Assert.That(JsonNode.Parse(handler.Body!)!["embeds"]![0]!["title"]!.GetValue<string>(), Does.Contain("B7"));
        Assert.That((await BugReportForwarder.SendAsync("https://example.com/x", report, null, false, client).ConfigureAwait(false)).Ok, Is.False);
    }

    /// <summary>
    /// The developer's hub gets the report with this computer's id, the version and the licence file, never the login;
    /// a refusal for the daily limit is told in words; without a machine id nothing is sent.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    [NonParallelizable]
    public async Task TheHubGetsTheReportWithTheLicenceNotTheLoginAsync()
    {
        var report = Report("Skills show no effect");
        report.Id = "B9";
        report.Character = "HeroDk";
        var message = BugReportsPlugIn.HubMessage(report, shareCharacter: false);
        var previous = (GameLogic.DeveloperHub.MachineId, GameLogic.DeveloperHub.LicenceFile);
        try
        {
            GameLogic.DeveloperHub.MachineId = string.Empty;
            Assert.That((await GameLogic.DeveloperHub.SendAsync(message, url: "https://hub.example").ConfigureAwait(false)).Ok, Is.False);

            GameLogic.DeveloperHub.MachineId = "8e5abdd396d53501";
            GameLogic.DeveloperHub.LicenceFile = "{\"payload\":\"x\",\"signature\":\"y\"}";
            var handler = new RecordingHandler();
            using var client = new HttpClient(handler);
            var (ok, _, _) = await GameLogic.DeveloperHub.SendAsync(message, client, "https://hub.example/").ConfigureAwait(false);
            Assert.That(ok, Is.True);
            Assert.That(handler.Url, Is.EqualTo("https://hub.example/api/report"));
            var sent = JsonNode.Parse(handler.Body!)!;
            Assert.That(sent["kind"]!.GetValue<string>(), Is.EqualTo("bug"));
            Assert.That(sent["machine"]!.GetValue<string>(), Is.EqualTo("8e5abdd396d53501"));
            Assert.That(sent["licence"]!.GetValue<string>(), Does.Contain("payload"));
            Assert.That(handler.Body, Does.Not.Contain("alice").And.Not.Contain("HeroDk"));

            handler.Status = (HttpStatusCode)429;
            var limited = await GameLogic.DeveloperHub.SendAsync(BugReportsPlugIn.HubMessage(report, false), client, "https://hub.example").ConfigureAwait(false);
            Assert.That(limited.Ok, Is.False);
            Assert.That(limited.Vi, Does.Contain("hết lượt"));
        }
        finally
        {
            (GameLogic.DeveloperHub.MachineId, GameLogic.DeveloperHub.LicenceFile) = previous;
        }
    }

    /// <summary>A player sends a report from the game; the window's state lists it and the reports left.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task APlayerSendsAReportAsync()
    {
        var context = GameContextTestHelper.CreateGameContext([]);
        var player = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        player.Account = new HNMO.Persistence.BasicModel.Account { LoginName = "carol" };
        var plugIn = new BugReportsPlugIn { Configuration = new BugReportsConfiguration { FileName = string.Empty } };

        var (en, _, sent) = plugIn.Submit(player, "skill", "Twisting Slash has no effect", "version 1\npassword=secret\nbrowser x");
        Assert.That(sent, Is.True, en);
        var saved = plugIn.All().Single();
        Assert.That(saved.Info, Does.Not.Contain("secret"));
        Assert.That(saved.Login, Is.EqualTo("carol"));
        var state = System.Text.Json.JsonSerializer.Serialize(plugIn.StateOf(player));
        Assert.That(state, Does.Contain("\"left\":4").And.Contain("Twisting Slash"));
    }

    private static BugReport Report(string text, string category = "display") => new() { Login = "alice", Text = text, Category = category };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }

        public string? Url { get; private set; }

        public HttpStatusCode Status { get; set; } = HttpStatusCode.NoContent;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.Url = request.RequestUri?.ToString();
            this.Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(this.Status);
        }
    }
}
