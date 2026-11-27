// Unit tests of the updater's rules (bridge/Updater.cs): which version is newer, which files an update brings and
// takes away, and that a manifest can't reach outside the HNMO folder or into the save game.
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace HNMO.Launcher.Tests;

[TestFixture]
public class UpdaterTests
{
    [TestCase("1.0.1", "1.0.0", true)]
    [TestCase("1.0.10", "1.0.9", true, Description = "numbers, not text")]
    [TestCase("1.1.0", "1.0.99", true)]
    [TestCase("1.0.0", "1.0.0", false)]
    [TestCase("0.9.0", "1.0.0", false)]
    [TestCase(null, "1.0.0", false)]
    [TestCase("latest", "1.0.0", false)]
    public void NewerVersions(string? offered, string running, bool newer) => Assert.That(HnmoUpdater.IsNewer(offered, running), Is.EqualTo(newer));

    [Test]
    public void AnUpdateBringsTheChangedFilesAndTakesAwayTheGoneOnes()
    {
        var installed = new UpdateManifest("1.0.0", null, [new("server/a.dll", 1, "aa"), new("web/b.js", 1, "bb"), new("old.txt", 1, "cc")]);
        var next = new UpdateManifest("1.0.1", null, [new("server/a.dll", 1, "AA"), new("web/b.js", 1, "b2"), new("new.txt", 1, "dd")]);
        var (copies, removes) = HnmoUpdater.Diff(installed, next);
        Assert.That(copies.Select(c => c.Path), Is.EquivalentTo(new[] { "web/b.js", "new.txt" }), "the same hash in other letters is the same file");
        Assert.That(removes, Is.EqualTo(new[] { "old.txt" }));
    }

    [Test]
    public void AManifestStaysInsideTheFolderAndOutOfTheSaveGame()
    {
        var root = Path.Combine(Path.GetTempPath(), "hnmo-root");
        Assert.That(HnmoUpdater.SafePath(root, "server/HNMO.Startup.dll"), Is.EqualTo(Path.Combine(root, "server", "HNMO.Startup.dll")));
        Assert.That(HnmoUpdater.SafePath(root, "../outside.txt"), Is.Null);
        Assert.That(HnmoUpdater.SafePath(root, "server/../../outside.txt"), Is.Null);
        Assert.That(HnmoUpdater.SafePath(root, "save/hnmo-settings.json"), Is.Null, "the save game is never touched");
        Assert.That(HnmoUpdater.SafePath(root, "web/../save/x"), Is.Null);
        Assert.That(HnmoUpdater.SafePath(root, "/etc/passwd"), Is.Null);
        Assert.That(HnmoUpdater.SafePath(root, ""), Is.Null);
    }

    [Test]
    public async Task AFilesHashIsItsSha256()
    {
        var file = Path.GetTempFileName();
        await File.WriteAllTextAsync(file, "HNMO");
        Assert.That(await HnmoUpdater.HashAsync(file), Is.EqualTo(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData("HNMO"u8)).ToLowerInvariant()));
        File.Delete(file);
    }

    [Test]
    public void ABackupLeavesOutTheUpdaterAndARestoreNeverWritesIt()
    {
        var save = Path.Combine(Path.GetTempPath(), "hnmo-save-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(save, "updates", "runner"));
        File.WriteAllText(Path.Combine(save, "updates", "runner", "HNMO"), "runner");
        File.WriteAllText(Path.Combine(save, "hnmo-settings.json"), "{}");
        try
        {
            var backup = HNMO.Interfaces.ControlOperations.CreateBackupZip(save);
            var zipPath = Path.Combine(save, "backups", backup.FileName);
            using (var zip = System.IO.Compression.ZipFile.OpenRead(zipPath))
            {
                Assert.That(zip.Entries.Select(e => e.FullName), Is.EquivalentTo(new[] { "save/hnmo-settings.json" }));
            }

            // A backup from before this rule, holding the updater's helper: the restore leaves the running helper alone.
            using (var zip = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Update))
            {
                using var writer = new StreamWriter(zip.CreateEntry("save/updates/runner/HNMO").Open());
                writer.Write("old runner");
            }

            HNMO.Interfaces.ControlOperations.RestoreBackupZip(save, backup.FileName);
            Assert.That(File.ReadAllText(Path.Combine(save, "updates", "runner", "HNMO")), Is.EqualTo("runner"));
        }
        finally
        {
            Directory.Delete(save, recursive: true);
        }
    }

    [Test]
    public async Task ValidLicenceAppliesHubReturnedFilesWithdrawnAndUnreachableKeepCurrentFilesAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "hnmo-updater-" + Guid.NewGuid().ToString("N"));
        var save = Path.Combine(root, "save");
        Directory.CreateDirectory(Path.Combine(root, "server"));
        Directory.CreateDirectory(save);
        try
        {
            var oldBytes = Encoding.UTF8.GetBytes("current-dll");
            var oldHash = Convert.ToHexString(SHA256.HashData(oldBytes)).ToLowerInvariant();
            File.WriteAllBytes(Path.Combine(root, "server", "app.dll"), oldBytes);
            File.WriteAllText(Path.Combine(root, "version.json"), """{"version":"1.0.0"}""");
            File.WriteAllText(Path.Combine(root, "manifest.json"), JsonSerializer.Serialize(new UpdateManifest("1.0.0", "HNMO-test-stamp", [new("server/app.dll", oldBytes.Length, oldHash)])));

            // 1) No licence: no /api/update-check request, no update offered, current files kept.
            var noLicenceHandler = new StubHubHandler((_, _) => throw new AssertionException("Hub must not be contacted without a licence."));
            var updater = new HnmoUpdater(root, save, 8080, 8095, new StubClientFactory(noLicenceHandler), NullLogger.Instance, () => "aaaa1111bbbb2222", () => "cccc3333dddd4444");
            var noLicStatus = await updater.StatusAsync(true, CancellationToken.None).ConfigureAwait(false);
            Assert.That(noLicStatus.Available, Is.False);
            Assert.That(File.ReadAllBytes(Path.Combine(root, "server", "app.dll")), Is.EqualTo(oldBytes));

            // Write licence.hnmo
            var payloadJson = """{"licensee":"Minh","maxPlayers":40,"machine":"aaaa1111bbbb2222","id":"L-update-1"}""";
            var licenceText = $$"""{"payload":"{{Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson))}}","signature":"c2ln"}""";
            File.WriteAllText(Path.Combine(root, "license.hnmo"), licenceText);

            // 2) Withdrawn licence (403): no update offered, no downloads, current files kept.
            var withdrawnDownloads = 0;
            var withdrawnHandler = new StubHubHandler((req, _) =>
            {
                if (req.RequestUri!.AbsolutePath == "/api/update-check")
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("""{"error":"forbidden","reason":"withdrawn"}""") });
                }

                withdrawnDownloads++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            });
            var withdrawnUpdater = new HnmoUpdater(root, save, 8080, 8095, new StubClientFactory(withdrawnHandler), NullLogger.Instance, () => "aaaa1111bbbb2222", () => "cccc3333dddd4444");
            var withdrawnStatus = await withdrawnUpdater.StatusAsync(true, CancellationToken.None).ConfigureAwait(false);
            Assert.That(withdrawnStatus.Available, Is.False);
            Assert.That(withdrawnDownloads, Is.Zero);
            Assert.That(File.ReadAllBytes(Path.Combine(root, "server", "app.dll")), Is.EqualTo(oldBytes));

            // 3) Hub unreachable: keeps current files, never throws, never wipes.
            var unreachableHandler = new StubHubHandler((_, _) => throw new HttpRequestException("offline"));
            var unreachableUpdater = new HnmoUpdater(root, save, 8080, 8095, new StubClientFactory(unreachableHandler), NullLogger.Instance, () => "aaaa1111bbbb2222", () => "cccc3333dddd4444");
            var unreachableStatus = await unreachableUpdater.StatusAsync(true, CancellationToken.None).ConfigureAwait(false);
            Assert.That(unreachableStatus.Available, Is.False);
            Assert.That(File.ReadAllBytes(Path.Combine(root, "server", "app.dll")), Is.EqualTo(oldBytes));

            // 4) Valid licence: /api/update-check returns files + token; only hub-returned changed files are fetched with Bearer token.
            var newBytes = Encoding.UTF8.GetBytes("updated-dll-v2");
            var newHash = Convert.ToHexString(SHA256.HashData(newBytes)).ToLowerInvariant();
            string? checkBody = null;
            string? authHeader = null;
            string? machineHeader = null;
            var validHandler = new StubHubHandler(async (req, ct) =>
            {
                if (req.RequestUri!.AbsolutePath == "/api/update-check")
                {
                    checkBody = await req.Content!.ReadAsStringAsync(ct).ConfigureAwait(false);
                    var answer = $$"""
                    {
                      "manifestVersion": "1.0.1",
                      "notes": "Fix",
                      "downloadBase": "https://hub.example/api/update-file",
                      "token": "short-lived-tok",
                      "files": [{"path": "server/app.dll", "sha256": "{{newHash}}", "size": {{newBytes.Length}}, "exec": false}]
                    }
                    """;
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer) };
                }

                if (req.RequestUri.ToString() == "https://hub.example/api/update-file?path=server%2Fapp.dll")
                {
                    authHeader = req.Headers.Authorization?.ToString();
                    machineHeader = req.Headers.TryGetValues("X-HNMO-Machine", out var vals) ? vals.FirstOrDefault() : null;
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(newBytes) };
                }

                throw new AssertionException("Unexpected URL: " + req.RequestUri);
            });

            var validUpdater = new HnmoUpdater(root, save, 8080, 8095, new StubClientFactory(validHandler), NullLogger.Instance, () => "aaaa1111bbbb2222", () => "cccc3333dddd4444");
            var status = await validUpdater.StatusAsync(true, CancellationToken.None).ConfigureAwait(false);
            Assert.That(status.Available, Is.True);
            Assert.That(status.Latest, Is.EqualTo("1.0.1"));
            var sent = JsonNode.Parse(checkBody!)!;
            Assert.That(sent["licenceId"]!.GetValue<string>(), Is.EqualTo("L-update-1"));
            Assert.That(sent["machineId"]!.GetValue<string>(), Is.EqualTo("aaaa1111bbbb2222"));
            Assert.That(sent["buildStamp"]!.GetValue<string>(), Is.EqualTo("HNMO-test-stamp"));
            Assert.That(Encoding.UTF8.GetString(Convert.FromBase64String(sent["licence"]!.GetValue<string>())), Is.EqualTo(licenceText));

            var plan = await validUpdater.StageAuthorizedUpdateForTestAsync(CancellationToken.None).ConfigureAwait(false);
            Assert.That(plan, Is.Not.Null);
            Assert.That(authHeader, Is.EqualTo("Bearer short-lived-tok"));
            Assert.That(machineHeader, Is.EqualTo("aaaa1111bbbb2222"));
            Assert.That(plan!.Copies.Select(c => c.Path), Is.EqualTo(new[] { "server/app.dll" }));
            Assert.That(File.ReadAllBytes(Path.Combine(plan.StagingFolder, newHash)), Is.EqualTo(newBytes));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class StubClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubHubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => respond(request, cancellationToken);
    }
}
