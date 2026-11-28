// <copyright file="AutoErrorReportsTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns;
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
        Assert.That(AutoErrorEngine.Find(data, "E1")!.Count, Is.EqualTo(5), "dropped reports aren't counted");

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

    /// <summary>Private data is absent from stored diagnostics and the approved snapshot.</summary>
    [Test]
    public void DropsClientControlledText()
    {
        const string secret = "person@example.invalid 192.0.2.7 Bearer fakeJwt /home/PrivateName";
        var data = new BugReportStoreData();
        var sample = new AutoErrorSample
        {
            Kind = "error", Message = secret, Stack = secret, Character = secret, Browser = secret,
            Version = secret, Os = secret, Map = secret, Device = secret, Gpu = secret,
        };
        var (_, group) = AutoErrorEngine.Submit(data, "account", $"{secret}|/private/main.js|12", sample, new BugReportsConfiguration(), DateTime.UtcNow, Sydney);
        Assert.That(group!.Fingerprint, Is.EqualTo("error|main.js|12"));
        foreach (var fragment in new[] { "example.invalid", "192.0.2.7", "fakeJwt", "PrivateName" })
        {
            Assert.That(JsonSerializer.Serialize(group), Does.Not.Contain(fragment));
            Assert.That(AutoErrorEngine.HubMessage(group, "s9").ToJsonString(), Does.Not.Contain(fragment));
        }
    }

    /// <summary>Checks both supported wire encodings and rejects malformed payloads before persistence.</summary>
    [Test]
    public void ValidatesPacketWithoutRewritingHeader()
    {
        const string valid = """{"kind":"error","fingerprint":"failure|/main.js|12","stack":"fixture"}""";
        foreach (var flag in new[] { false, true })
        {
            var packet = Packet(valid, flag);
            Assert.That(AutoErrorPayload.TryReadPacket(packet, out _, out _), Is.True);
            packet[2] ^= 1;
            var bad = packet.ToArray();
            Assert.That(AutoErrorPayload.TryReadPacket(packet, out _, out _), Is.False);
            Assert.That(packet, Is.EqualTo(bad));
        }

        foreach (var json in new[]
        {
            "[]", "{", """{"kind":4,"fingerprint":"a"}""", """{"kind":"error","kind":"packet","fingerprint":"a"}""",
            """{"kind":"error","fingerprint":"a","map":{}}""", """{"kind":"other","fingerprint":"a"}""",
            """{"kind":"error","fingerprint":"a","season":"s9"}""", valid + "\0" + valid,
            new string(' ', AutoErrorPayload.MaxJsonBytes + 1),
        })
        {
            Assert.That(AutoErrorPayload.TryReadPacket(Packet(json, false), out _, out _), Is.False);
        }
    }

    /// <summary>Concurrent GameServers using one state file share the cap and a cold disk read sees the accepted reports.</summary>
    [Test]
    public void ConcurrentStoreUpdatesSurviveReopen()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(dir.FullName, "test-store.json");
            var resolved = HnmoStateStore.ResolveStateFilePath(path)!;
            var stores = new[] { BugReportStore.Open(path), BugReportStore.Open(path) };
            var settings = new BugReportsConfiguration { AutomaticDailyLimitPerAccount = 5, AutomaticDailyLimitPerServer = 11 };
            var now = DateTime.UtcNow;
            System.Threading.Tasks.Parallel.For(0, 60, i =>
            {
                stores[i % 2].Mutate(data =>
                {
                    var (_, group) = AutoErrorEngine.Submit(data, $"account-{i % 3}", "error", Sample(), settings, now, Sydney);
                    return (group is not null, true);
                });
            });

            var restored = new HnmoStateStore(resolved).ReadSection<BugReportStoreData>("bugReports", new JsonSerializerOptions())!;
            Assert.That(restored.AutoGroups.Single().Count, Is.EqualTo(11));
            Assert.That(restored.AutoAccountCounts.Values.All(count => count <= 5), Is.True);
            Assert.That(restored.AutoServerCount, Is.EqualTo(11));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>Only groups explicitly approved for their own season enter the outbox.</summary>
    [Test]
    public void ApprovalsAreIndependentAcrossSeasons()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var s6 = new AutoErrorGroup { ApprovalId = Guid.NewGuid(), Id = "E1", Kind = "error" };
            var s9 = new AutoErrorGroup { ApprovalId = Guid.NewGuid(), Id = "E1", Kind = "webgl", Count = 2 };
            Assert.That(AutoErrorOutbox.ReadPending(dir.FullName), Is.Empty);
            Assert.That(AutoErrorOutbox.TryApprove(s6, "s6", dir.FullName), Is.True);
            Assert.That(AutoErrorOutbox.TryApprove(s9, "s9", dir.FullName), Is.True);
            Assert.That(AutoErrorOutbox.TryApprove(s9, "s9", dir.FullName), Is.False);
            s9.Count++;
            var batch = AutoErrorOutbox.ReadPending(dir.FullName);
            Assert.That(batch.Select(e => e.Season), Is.EquivalentTo(new[] { "s6", "s9" }));
            Assert.That((int?)batch.Single(e => e.Id == s9.ApprovalId).Message["count"], Is.EqualTo(2));

            AutoErrorOutbox.MarkAccepted(batch, new HashSet<Guid>(), dir.FullName);
            Assert.That(AutoErrorOutbox.ReadPending(dir.FullName), Has.Count.EqualTo(2));
            AutoErrorOutbox.MarkAccepted(batch, new HashSet<Guid> { s9.ApprovalId, Guid.NewGuid() }, dir.FullName);
            Assert.That(AutoErrorOutbox.ReadPending(dir.FullName).Single().Id, Is.EqualTo(s6.ApprovalId));
            Assert.That(AutoErrorOutbox.TryApprove(s9, "s9", dir.FullName), Is.False);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>A successful response from an old hub leaves auto-errors queued until there is a per-item acknowledgement.</summary>
    [Test]
    [NonParallelizable]
    public async Task WeeklyHubAcknowledgementIsPerApprovalAsync()
    {
        const string keyVariable = "HNMO_AUTO_ERROR_FORWARD_KEY";
        const string hubVariable = "HNMO_DEVELOPER_HUB";
        var oldKey = Environment.GetEnvironmentVariable(keyVariable);
        var oldHub = Environment.GetEnvironmentVariable(hubVariable);
        var oldMachine = DeveloperHub.MachineId;
        var oldPath = DeveloperHub.StateFile;
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            Environment.SetEnvironmentVariable(hubVariable, "http://127.0.0.1:57841");
            Environment.SetEnvironmentVariable(keyVariable, new string('a', 32));
            DeveloperHub.MachineId = "1234567890abcdef";
            DeveloperHub.StateFile = Path.Combine(dir.FullName, "hub.json");
            var id = Guid.NewGuid();
            using var handler = new MockHubHandler();
            using var client = new HttpClient(handler);
            IReadOnlySet<Guid>? accepted = null;
            JsonObject Weekly() => new() { ["autoErrors"] = new JsonArray(new JsonObject { ["approval"] = id.ToString("N") }) };

            var first = await DeveloperHub.SendWeeklyAsync(Weekly(), client, ids => accepted = ids);
            Assert.That(first.Ok, Is.True);
            Assert.That(accepted, Is.Null);
            handler.Body = $$"""{"ok":true,"autoAccepted":["{{id:N}}","invalid"]}""";
            var second = await DeveloperHub.SendWeeklyAsync(Weekly(), client, ids => accepted = ids);
            Assert.That(second.Ok, Is.True);
            Assert.That(accepted, Is.EquivalentTo(new[] { id }));
        }
        finally
        {
            Environment.SetEnvironmentVariable(keyVariable, oldKey);
            Environment.SetEnvironmentVariable(hubVariable, oldHub);
            DeveloperHub.MachineId = oldMachine;
            DeveloperHub.StateFile = oldPath;
            dir.Delete(recursive: true);
        }
    }

    private static byte[] Packet(string json, bool terminated)
    {
        var payload = Encoding.UTF8.GetBytes(json);
        var packet = new byte[5 + payload.Length + (terminated ? 1 : 0)];
        packet[0] = 0xC2;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(1), (ushort)packet.Length);
        packet[3] = 0xF5;
        packet[4] = 0x60;
        payload.CopyTo(packet, 5);
        return packet;
    }

    private sealed class MockHubHandler : HttpMessageHandler
    {
        public string Body { get; set; } = """{"ok":true}""";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.That(request.RequestUri!.Host, Is.EqualTo("127.0.0.1"));
            Assert.That(request.RequestUri.AbsolutePath, Is.EqualTo("/api/weekly"));
            Assert.That(request.Headers.Contains("X-Hnmo-Auto-Error-Key"), Is.True);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(this.Body) });
        }
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
