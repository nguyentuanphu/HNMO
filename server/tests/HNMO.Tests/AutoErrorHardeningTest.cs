// <copyright file="AutoErrorHardeningTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Buffers.Binary;
using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.BugReports;
using HNMO.Network.Packets.ClientToServer;

/// <summary>
/// Edge cases of the automatic error reports (C2 F5 60): both writers, broken headers and sizes, odd JSON, the caps
/// around a Sydney midnight under concurrency, and the clean-up of a state file written by an older version.
/// </summary>
[TestFixture]
public sealed class AutoErrorHardeningTest
{
    private const string Secret = "person@example.invalid 192.0.2.7 Bearer fakeJwt /home/PrivateName ticket=abc123";
    private static readonly TimeZoneInfo Sydney = GameLogic.PlugIns.News.NewsStore.ResolveSydneyZone();

    /// <summary>The generated C# writer ends the JSON with one NUL; the parser reads it like the web writer's.</summary>
    [Test]
    public void GeneratedWriterIsRead()
    {
        const string json = """{"kind":"error","fingerprint":"error|main.js|12","version":"web 20261127","browser":"Chrome 131","os":"Linux"}""";
        var bytes = new byte[HnmoAutoErrorReportRef.GetRequiredSize(json)];
        var packet = new HnmoAutoErrorReportRef(bytes) { Json = json };
        Assert.That(packet.Header.Length, Is.EqualTo(bytes.Length));
        Assert.That(bytes[0], Is.EqualTo(0xC2));
        Assert.That(bytes[3..5], Is.EqualTo(new byte[] { 0xF5, 0x60 }));
        Assert.That(bytes[^1], Is.EqualTo(0), "one NUL after the JSON");
        Assert.That(bytes[^2], Is.Not.EqualTo(0));

        Assert.That(AutoErrorPayload.TryReadPacket(bytes, out var fingerprint, out var sample), Is.True);
        Assert.That(fingerprint, Is.EqualTo("error|main.js|12"));
        Assert.That(sample.Browser, Is.EqualTo("Chrome 131"));
        Assert.That(AutoErrorPayload.TryReadPacket(Packet(json, nulls: 0), out _, out _), Is.True, "web writer: no terminator");
        Assert.That(AutoErrorPayload.TryReadPacket(Packet(json, nulls: 2), out _, out _), Is.False, "two NULs");
    }

    /// <summary>A packet with another header, code, subcode or a wrong length is refused.</summary>
    /// <param name="index">The byte to change.</param>
    /// <param name="value">Its new value.</param>
    [TestCase(0, 0xC1)]
    [TestCase(0, 0xC3)]
    [TestCase(0, 0xC4)]
    [TestCase(3, 0xF4)]
    [TestCase(4, 0x61)]
    [TestCase(4, 0x52)]
    public void WrongHeaderIsRefused(int index, int value)
    {
        var packet = Packet("""{"kind":"error","fingerprint":"a"}""");
        packet[index] = (byte)value;
        Assert.That(AutoErrorPayload.TryReadPacket(packet, out _, out _), Is.False);
    }

    /// <summary>The length field must be the packet's length; a header alone or a cut packet is refused.</summary>
    [Test]
    public void WrongLengthIsRefused()
    {
        var packet = Packet("""{"kind":"error","fingerprint":"a"}""");
        foreach (var delta in new[] { -1, 1, 300 })
        {
            var copy = packet.ToArray();
            BinaryPrimitives.WriteUInt16BigEndian(copy.AsSpan(1), (ushort)(packet.Length + delta));
            Assert.That(AutoErrorPayload.TryReadPacket(copy, out _, out _), Is.False, $"length {delta:+#;-#}");
        }

        Assert.That(AutoErrorPayload.TryReadPacket(packet.AsMemory(0, 5), out _, out _), Is.False, "header only");
        Assert.That(AutoErrorPayload.TryReadPacket(packet.AsMemory(0, 12), out _, out _), Is.False, "cut packet");
        Assert.That(AutoErrorPayload.TryReadPacket(ReadOnlyMemory<byte>.Empty, out _, out _), Is.False);
    }

    /// <summary>16 KiB of JSON is the most, with or without the writer's NUL.</summary>
    [Test]
    public void SixteenKibIsTheLimit()
    {
        var max = Json(AutoErrorPayload.MaxJsonBytes);
        Assert.That(System.Text.Encoding.UTF8.GetByteCount(max), Is.EqualTo(AutoErrorPayload.MaxJsonBytes));
        Assert.That(AutoErrorPayload.TryReadPacket(Packet(max, nulls: 0), out _, out _), Is.True);
        Assert.That(AutoErrorPayload.TryReadPacket(Packet(max, nulls: 1), out _, out _), Is.True);
        var over = Json(AutoErrorPayload.MaxJsonBytes + 1);
        Assert.That(AutoErrorPayload.TryReadPacket(Packet(over, nulls: 0), out _, out _), Is.False);
        Assert.That(AutoErrorPayload.TryReadPacket(Packet(over, nulls: 1), out _, out _), Is.False);
    }

    /// <summary>JSON that isn't one flat object of known string fields, each once, is refused.</summary>
    /// <param name="json">The JSON.</param>
    [TestCase("")]
    [TestCase("null")]
    [TestCase("\"text\"")]
    [TestCase("{}")]
    [TestCase("""{"kind":"error"}""")]
    [TestCase("""{"fingerprint":"a"}""")]
    [TestCase("""{"kind":"error","fingerprint":""}""")]
    [TestCase("""{"kind":"error","fingerprint":"   "}""")]
    [TestCase("""{"kind":"error","fingerprint":"a"} trailing""")]
    [TestCase("""{"kind":"error","fingerprint":"a"}{"kind":"error"}""")]
    [TestCase("""{"kind":"error","fingerprint":"a",}""")]
    [TestCase("""{"kind":"error","fingerprint":null}""")]
    [TestCase("""{"kind":"error","fingerprint":["a"]}""")]
    [TestCase("""{"kind":"error","fingerprint":"a","os":7}""")]
    [TestCase("""{"kind":"error","fingerprint":"a","Kind":"error"}""")]
    [TestCase("""{"kind":"error","fingerprint":"a","kind":"packet"}""")]
    [TestCase("""{"kind":"error","fingerprint":"a","fingerprint":"b"}""")]
    [TestCase("""{"kind":"error","fingerprint":"a","character":"x"}""")]
    [TestCase("""{"kind":"error","fingerprint":"a","login":"x"}""")]
    public void OddJsonIsRefused(string json)
    {
        Assert.That(AutoErrorPayload.TryReadPacket(Packet(json), out _, out _), Is.False);
    }

    /// <summary>A fingerprint of more than 512 characters is refused; invalid UTF-8 too.</summary>
    [Test]
    public void LongFingerprintAndInvalidUtf8AreRefused()
    {
        Assert.That(AutoErrorPayload.TryReadPacket(Packet($$"""{"kind":"error","fingerprint":"{{new string('a', 512)}}"}"""), out _, out _), Is.True);
        Assert.That(AutoErrorPayload.TryReadPacket(Packet($$"""{"kind":"error","fingerprint":"{{new string('a', 513)}}"}"""), out _, out _), Is.False);
        var bytes = Packet("""{"kind":"error","fingerprint":"aa"}""");
        bytes[^3] = 0xFF;
        Assert.That(AutoErrorPayload.TryReadPacket(bytes, out _, out _), Is.False);
    }

    /// <summary>
    /// Two GameServers on one state file take reports at once across a Sydney midnight: each Sydney day gets exactly its
    /// cap, and the caps of the configuration are bounded (100 an account, 10,000 a server).
    /// </summary>
    [Test]
    public void ConcurrentReportsAroundSydneyMidnightKeepEachDaysCap()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(dir.FullName, HnmoStateStore.StateFileName);
            var stores = new[] { BugReportStore.Open(path), BugReportStore.Open(path) };
            var settings = new BugReportsConfiguration { AutomaticDailyLimitPerAccount = 1_000, AutomaticDailyLimitPerServer = 30 };

            // 23:59:59.000 Sydney (summer time, +11) on 27 Nov 2026; each report 10 ms after the one before.
            var start = new DateTime(2026, 11, 27, 12, 59, 59, DateTimeKind.Utc);
            long tick = -1;
            var outcomes = new System.Collections.Concurrent.ConcurrentBag<(string Day, AutoErrorEngine.Outcome Outcome)>();
            System.Threading.Tasks.Parallel.For(0, 200, i =>
            {
                stores[i % 2].Mutate(data =>
                {
                    // The time is taken under the lock, as BugReportsPlugIn.SubmitAutomatic does.
                    var now = start.AddMilliseconds(10 * System.Threading.Interlocked.Increment(ref tick));
                    var (outcome, group) = AutoErrorEngine.Submit(data, $"account-{i % 4}", "error|main.js|12", Sample(), settings, now, Sydney);
                    outcomes.Add((TimeZoneInfo.ConvertTimeFromUtc(now, Sydney).ToString("yyyy-MM-dd"), outcome));
                    return (group is not null, true);
                });
            });

            var accepted = outcomes.Where(o => o.Outcome is AutoErrorEngine.Outcome.Added or AutoErrorEngine.Outcome.NewGroup)
                .GroupBy(o => o.Day).ToDictionary(g => g.Key, g => g.Count());
            Assert.That(accepted, Is.EquivalentTo(new Dictionary<string, int> { ["2026-11-27"] = 30, ["2026-11-28"] = 30 }));
            Assert.That(outcomes.Count(o => o.Outcome == AutoErrorEngine.Outcome.ServerCap), Is.EqualTo(200 - 60));

            var restored = new HnmoStateStore(HnmoStateStore.ResolveStateFilePath(path)!).ReadSection<BugReportStoreData>("bugReports", new JsonSerializerOptions())!;
            Assert.That(restored.AutoDay, Is.EqualTo("2026-11-28"));
            Assert.That(restored.AutoServerCount, Is.EqualTo(30));
            Assert.That(restored.AutoGroups.Single().Count, Is.EqualTo(60));

            var bounded = new BugReportStoreData();
            var huge = new BugReportsConfiguration { AutomaticDailyLimitPerAccount = int.MaxValue, AutomaticDailyLimitPerServer = int.MaxValue };
            var results = Enumerable.Range(0, 101).Select(_ => AutoErrorEngine.Submit(bounded, "one", "error", Sample(), huge, start, Sydney).Outcome).ToList();
            Assert.That(results[^1], Is.EqualTo(AutoErrorEngine.Outcome.AccountCap), "100 an account at most");
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>
    /// A state file of an older version (raw message, stack, GPU, map, character, no approval id, marked as sent) is
    /// cleaned on its first read: the file and its .bak keep none of the old text, and the groups need a new approval.
    /// </summary>
    [Test]
    public void LegacyStateIsCleanedInTheFileAndItsBackup()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(dir.FullName, HnmoStateStore.StateFileName);
            var legacy = new BugReportStoreData { AutoDay = "2026-11-26", NextAutoNumber = 2 };
            legacy.AutoGroups.Add(new AutoErrorGroup
            {
                Id = "E1", Kind = "error", Fingerprint = $"{Secret}|/home/PrivateName/main.js|12", Message = Secret,
                Versions = ["1.0.0", Secret], Accounts = ["alice"], Count = 3,
                QueuedUtc = DateTime.UtcNow, ForwardedUtc = DateTime.UtcNow,
                Samples = [new AutoErrorSample { Kind = "error", Message = Secret, Stack = Secret, Gpu = Secret, Map = Secret, Device = Secret, Character = Secret, Browser = Secret, Os = Secret, Version = Secret }],
            });
            new HnmoStateStore(HnmoStateStore.ResolveStateFilePath(path)!).WriteSection("bugReports", legacy, new JsonSerializerOptions());
            Assert.That(File.ReadAllText(path), Does.Contain("PrivateName"), "the fixture holds the old text");

            var groups = BugReportStore.Open(path).Read(data => data.AutoGroups.ToList());
            var group = groups.Single();
            Assert.That(group.ApprovalId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(group.QueuedUtc, Is.Null);
            Assert.That(group.ForwardedUtc, Is.Null);
            Assert.That(group.Fingerprint, Is.EqualTo("error|main.js|12"));
            Assert.That(group.Versions, Is.EqualTo(new[] { "1.0.0" }));

            foreach (var file in new[] { path, path + ".bak" })
            {
                var text = File.ReadAllText(file);
                foreach (var fragment in new[] { "example.invalid", "192.0.2.7", "fakeJwt", "PrivateName", "abc123" })
                {
                    Assert.That(text, Does.Not.Contain(fragment), $"{Path.GetFileName(file)} keeps {fragment}");
                }
            }
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>Everything the client controls stays out of the store, the outbox file and the hub message.</summary>
    [Test]
    public void FakePrivateDataStaysOutOfStoreOutboxAndHubMessage()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var json = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["kind"] = "error", ["fingerprint"] = $"{Secret}|https://h.example.invalid/js/main.js?ticket=abc123|12",
                ["message"] = Secret, ["stack"] = Secret, ["version"] = Secret, ["browser"] = Secret, ["os"] = Secret,
                ["device"] = Secret, ["map"] = Secret, ["gpu"] = Secret,
            });
            Assert.That(AutoErrorPayload.TryReadPacket(Packet(json), out var fingerprint, out var sample), Is.True);
            var store = BugReportStore.Open(Path.Combine(dir.FullName, HnmoStateStore.StateFileName));
            var group = store.Mutate(data =>
            {
                var (_, g) = AutoErrorEngine.Submit(data, "alice", fingerprint, sample, new BugReportsConfiguration(), DateTime.UtcNow, Sydney);
                return (g is not null, g);
            })!;
            Assert.That(group.Fingerprint, Is.EqualTo("error"), "a source with a query string isn't a known file: grouped by kind");
            Assert.That(AutoErrorOutbox.TryApprove(group, "s9", Path.Combine(dir.FullName, "auto-error-outbox")), Is.True);

            var texts = new List<string> { AutoErrorEngine.HubMessage(group, "s9").ToJsonString() };
            texts.AddRange(Directory.EnumerateFiles(dir.FullName, "*", SearchOption.AllDirectories).Select(File.ReadAllText));
            Assert.That(texts, Has.Count.GreaterThanOrEqualTo(3), "hub message, state file and outbox entry");
            foreach (var text in texts)
            {
                foreach (var fragment in new[] { "example.invalid", "192.0.2.7", "fakeJwt", "PrivateName", "abc123" })
                {
                    Assert.That(text, Does.Not.Contain(fragment));
                }
            }

            Assert.That(AutoErrorEngine.HubMessage(group, "s9").ToJsonString(), Does.Not.Contain("alice"), "no login goes out");
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>
    /// A wrong automatic error credential doesn't hold back the weekly report: after the hub's 403 the report goes
    /// again without the automatic errors (the players' reports with it), and nothing of the outbox counts as sent.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    [NonParallelizable]
    public async Task WrongCredentialStillSendsTheWeeklyAndPlayersReportsAsync()
    {
        const string keyVariable = "HNMO_AUTO_ERROR_FORWARD_KEY";
        const string hubVariable = "HNMO_DEVELOPER_HUB";
        var oldKey = Environment.GetEnvironmentVariable(keyVariable);
        var oldHub = Environment.GetEnvironmentVariable(hubVariable);
        var oldMachine = GameLogic.DeveloperHub.MachineId;
        var oldPath = GameLogic.DeveloperHub.StateFile;
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            Environment.SetEnvironmentVariable(hubVariable, "http://127.0.0.1:57841");
            Environment.SetEnvironmentVariable(keyVariable, new string('w', 32));
            GameLogic.DeveloperHub.MachineId = "1234567890abcdef";
            GameLogic.DeveloperHub.StateFile = Path.Combine(dir.FullName, "hub.json");
            var bodies = new List<(bool Key, System.Text.Json.Nodes.JsonObject Body)>();
            using var handler = new RecordingHandler(async request =>
            {
                var body = System.Text.Json.Nodes.JsonNode.Parse(await request.Content!.ReadAsStringAsync().ConfigureAwait(false))!.AsObject();
                var key = request.Headers.Contains("X-Hnmo-Auto-Error-Key");
                bodies.Add((key, body));
                return key
                    ? new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.Forbidden) { Content = new System.Net.Http.StringContent("""{"error":"automatic report credential"}""") }
                    : new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent("""{"ok":true,"bugs":1,"autoAccepted":["aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"]}""") };
            });
            using var client = new System.Net.Http.HttpClient(handler);
            var weekly = new System.Text.Json.Nodes.JsonObject
            {
                ["bugs"] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject { ["kind"] = "bug", ["id"] = "B1", ["text"] = "manual fixture report" }),
                ["autoErrors"] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject { ["approval"] = new string('a', 32) }),
            };
            var called = false;
            var (ok, _, error) = await GameLogic.DeveloperHub.SendWeeklyAsync(weekly, client, _ => called = true).ConfigureAwait(false);

            Assert.That(ok, Is.True, error);
            Assert.That(bodies.Select(b => b.Key), Is.EqualTo(new[] { true, false }));
            Assert.That(bodies[1].Body.ContainsKey("autoErrors"), Is.False);
            Assert.That(bodies[1].Body["bugs"]!.AsArray(), Has.Count.EqualTo(1));
            Assert.That(called, Is.False, "an answer to the report without automatic errors acknowledges none");
        }
        finally
        {
            Environment.SetEnvironmentVariable(keyVariable, oldKey);
            Environment.SetEnvironmentVariable(hubVariable, oldHub);
            GameLogic.DeveloperHub.MachineId = oldMachine;
            GameLogic.DeveloperHub.StateFile = oldPath;
            dir.Delete(recursive: true);
        }
    }

    private static string Json(int bytes)
    {
        const string head = "{\"kind\":\"error\",\"fingerprint\":\"error|main.js|12\",\"stack\":\"";
        const string tail = "\"}";
        return head + new string('x', bytes - head.Length - tail.Length) + tail;
    }

    private static byte[] Packet(string json, int nulls = 0)
    {
        var payload = System.Text.Encoding.UTF8.GetBytes(json);
        var packet = new byte[5 + payload.Length + nulls];
        packet[0] = 0xC2;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(1), (ushort)packet.Length);
        packet[3] = 0xF5;
        packet[4] = 0x60;
        payload.CopyTo(packet, 5);
        return packet;
    }

    private sealed class RecordingHandler(Func<System.Net.Http.HttpRequestMessage, Task<System.Net.Http.HttpResponseMessage>> answer) : System.Net.Http.HttpMessageHandler
    {
        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, System.Threading.CancellationToken cancellationToken) => answer(request);
    }

    private static AutoErrorSample Sample() => new() { Kind = "error", Version = "1.0.0", Browser = "Chrome 131", Os = "Windows" };
}
