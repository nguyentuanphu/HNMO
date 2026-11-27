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
    /// The developer's hub: a licence request carries a one-time secret, kept to fetch the answer; the issued licence
    /// comes back with it. The weekly report carries this computer's id, the version and the licence, never the login;
    /// a withdrawn licence is remembered. Without a machine id nothing is sent.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    [NonParallelizable]
    public async Task TheHubTakesRequestsAndWeeklyReportsAsync()
    {
        var file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"hnmo-hub-{Guid.NewGuid():N}.json");
        var previous = (GameLogic.DeveloperHub.MachineId, GameLogic.DeveloperHub.LicenceFile, GameLogic.DeveloperHub.StateFile);
        var previousHub = Environment.GetEnvironmentVariable("HNMO_DEVELOPER_HUB");
        Environment.SetEnvironmentVariable("HNMO_DEVELOPER_HUB", "https://hub.example");
        try
        {
            GameLogic.DeveloperHub.StateFile = file;
            GameLogic.DeveloperHub.MachineId = string.Empty;
            Assert.That((await GameLogic.DeveloperHub.RequestLicenceAsync("Minh", "zalo", 30, "hi").ConfigureAwait(false)).Ok, Is.False, "no machine id");

            GameLogic.DeveloperHub.MachineId = "8e5abdd396d53501";
            var licence = "{\"payload\":\"" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"licensee\":\"Minh\",\"maxPlayers\":30,\"id\":\"L-1\"}")) + "\",\"signature\":\"x\"}";
            var handler = new RecordingHandler { Answer = "{\"ok\":true,\"request\":\"R-1\"}", Status = HttpStatusCode.OK };
            using var client = new HttpClient(handler);
            var asked = await GameLogic.DeveloperHub.RequestLicenceAsync("Minh", "zalo", 30, "hi", client).ConfigureAwait(false);
            Assert.That(asked.Ok, Is.True, asked.En);
            Assert.That(handler.Url, Is.EqualTo("https://hub.example/api/request"));
            var request = GameLogic.DeveloperHub.ReadState().Request!;
            Assert.That(request.Id, Is.EqualTo("R-1"));
            Assert.That(handler.Body, Does.Contain(request.Token).And.Contain("8e5abdd396d53501"));

            handler.Answer = "{\"status\":\"issued\",\"licence\":" + System.Text.Json.JsonSerializer.Serialize(licence) + "}";
            var (status, issued) = await GameLogic.DeveloperHub.FetchLicenceAsync(client).ConfigureAwait(false);
            Assert.That((status, issued), Is.EqualTo(("issued", licence)));
            Assert.That(handler.Body, Does.Contain("R-1").And.Contain(request.Token));
            Assert.That(GameLogic.DeveloperHub.ReadState().Request!.Status, Is.EqualTo("issued"));
            Assert.That((await GameLogic.DeveloperHub.FetchLicenceAsync(client).ConfigureAwait(false)).Status, Is.Null, "asked once");

            GameLogic.DeveloperHub.LicenceFile = licence;
            Assert.That(GameLogic.DeveloperHub.LicenceIdOf(licence), Is.EqualTo("L-1"));
            var report = Report("Skills show no effect");
            report.Character = "HeroDk";
            var weekly = new JsonObject { ["players"] = new JsonObject { ["peak"] = 5 }, ["bugs"] = new JsonArray { BugReportsPlugIn.HubMessage(report, shareCharacter: false) } };
            handler.Answer = "{\"ok\":true,\"revoked\":true}";
            var sent = await GameLogic.DeveloperHub.SendWeeklyAsync(weekly, client).ConfigureAwait(false);
            Assert.That((sent.Ok, sent.Withdrawn), Is.EqualTo((true, true)));
            Assert.That(handler.Url, Is.EqualTo("https://hub.example/api/weekly"));
            Assert.That(handler.Body, Does.Contain("Skills show no effect").And.Contain("eyJsaWNlbnNlZSI6").And.Not.Contain("alice").And.Not.Contain("HeroDk"));
            Assert.That(GameLogic.DeveloperHub.IsWithdrawn(licence), Is.True, "remembered");
            Assert.That(GameLogic.DeveloperHub.ReadState().LastWeeklyUtc, Is.Not.Null);

            handler.Status = (HttpStatusCode)429;
            var limited = await GameLogic.DeveloperHub.SendWeeklyAsync(new JsonObject(), client).ConfigureAwait(false);
            Assert.That(limited.Ok, Is.False);
            Assert.That(limited.Error, Does.Contain("once a week"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("HNMO_DEVELOPER_HUB", previousHub); // a test run's "off" stays
            (GameLogic.DeveloperHub.MachineId, GameLogic.DeveloperHub.LicenceFile, GameLogic.DeveloperHub.StateFile) = previous;
            System.IO.File.Delete(file);
        }
    }

    /// <summary>A forwarded report waits for the weekly report, once; sent, it leaves the queue.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task AForwardedReportWaitsForTheWeeklyReportAsync()
    {
        var context = GameContextTestHelper.CreateGameContext([]);
        var player = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        player.Account = new HNMO.Persistence.BasicModel.Account { LoginName = "dave" };
        var plugIn = new BugReportsPlugIn { Configuration = new BugReportsConfiguration { FileName = string.Empty } };
        plugIn.Submit(player, "item", "The jewel vanished from the bag", null);
        var id = plugIn.All().Single().Id;
        Assert.That((await plugIn.ForwardAsync(id, null).ConfigureAwait(false)).Ok, Is.True);
        Assert.That((await plugIn.ForwardAsync(id, null).ConfigureAwait(false)).Ok, Is.False, "once");
        var queued = plugIn.QueuedForDeveloper();
        Assert.That(queued.Select(q => q.Id), Is.EqualTo(new[] { id }));
        Assert.That(queued[0].Message.ToJsonString(), Does.Not.Contain("dave"));
        plugIn.MarkSentToDeveloper([id]);
        Assert.That(plugIn.QueuedForDeveloper(), Is.Empty);
        Assert.That(plugIn.All().Single().ForwardedUtc, Is.Not.Null);
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

        public string Answer { get; set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.Url = request.RequestUri?.ToString();
            this.Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(this.Status) { Content = new StringContent(this.Answer) };
        }
    }
}
