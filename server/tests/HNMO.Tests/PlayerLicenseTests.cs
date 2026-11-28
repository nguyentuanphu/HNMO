// <copyright file="PlayerLicenseTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using HNMO.GameLogic;
using HNMO.Integrity;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Startup;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;

/// <summary>
/// The player limit's license (license.hnmo) and the count of players in the game.
/// </summary>
[TestFixture]
public class PlayerLicenseTests
{
    private static readonly DateOnly Today = new(2026, 11, 15);
    private Ed25519PrivateKeyParameters _privateKey = null!;
    private byte[] _publicKeyPem = null!;
    private string? _previousHub;

    /// <summary>A fresh keypair for each test.</summary>
    [SetUp]
    public void SetUp()
    {
        var generator = new Ed25519KeyPairGenerator();
        generator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
        var pair = generator.GenerateKeyPair();
        this._privateKey = (Ed25519PrivateKeyParameters)pair.Private;
        using var writer = new StringWriter();
        new PemWriter(writer).WriteObject(pair.Public);
        this._publicKeyPem = Encoding.UTF8.GetBytes(writer.ToString());
        LicensedPlayerLimit.Reset();

        // These tests talk to a stub hub: they need a hub address even when the test run turned the real one off.
        this._previousHub = Environment.GetEnvironmentVariable(DeveloperHubAddress.EnvironmentVariableName);
        Environment.SetEnvironmentVariable(DeveloperHubAddress.EnvironmentVariableName, "https://hub.example");
    }

    /// <summary>Gives the test run its hub setting back.</summary>
    [TearDown]
    public void TearDown() => Environment.SetEnvironmentVariable(DeveloperHubAddress.EnvironmentVariableName, this._previousHub);

    /// <summary>No file: the default of 10.</summary>
    [Test]
    public void WithoutLicenseTheDefaultApplies()
    {
        var license = PlayerLicense.Check(null, this._publicKeyPem, "abc", Today);
        Assert.That(license.IsValid, Is.False);
        Assert.That(license.MaximumPlayers, Is.EqualTo(10));
    }

    /// <summary>A licence that names its seasons covers only those; one that names none covers all (older licences).</summary>
    /// <param name="seasons">The licence's seasons field (JSON), or null for none.</param>
    /// <param name="season">This program's season.</param>
    /// <param name="valid">Whether it is used here.</param>
    [TestCase("""["s6"]""", "s6", true)]
    [TestCase("""["s6"]""", "s9", false)]
    [TestCase("""["s6","s9"]""", "s9", true)]
    [TestCase("""["s6","s9"]""", "arena", false)]
    [TestCase("""["s6","s9","arena"]""", "arena", true)]
    [TestCase(null, "s9", true)]
    [TestCase(null, "arena", true)]
    public void SeasonsOfALicence(string? seasons, string season, bool valid)
    {
        var field = seasons is null ? string.Empty : $",\"seasons\":{seasons}";
        var license = PlayerLicense.Check(this.File($$"""{"licensee":"Minh","maxPlayers":200,"expires":null,"machine":null{{field}}}"""), this._publicKeyPem, "abc", Today, season: season);
        Assert.That(license.IsValid, Is.EqualTo(valid));
        Assert.That(license.MaximumPlayers, Is.EqualTo(valid ? 200 : PlayerLicense.DefaultMaximumPlayers));
        if (seasons is not null)
        {
            Assert.That(license.Seasons, Is.Not.Null.And.Not.Empty, "the seasons are kept for the License page");
        }
    }

    /// <summary>A signed license raises the limit.</summary>
    [Test]
    public void SignedLicenseRaisesTheLimit()
    {
        var license = PlayerLicense.Check(this.File("""{"licensee":"Minh","maxPlayers":50,"expires":"2027-01-31","machine":null}"""), this._publicKeyPem, "abc", Today);
        Assert.That(license.IsValid, Is.True);
        Assert.That(license.MaximumPlayers, Is.EqualTo(50));
        Assert.That(license.Licensee, Is.EqualTo("Minh"));
    }

    /// <summary>A changed payload, an expired license, another computer or a license of another key: the default.</summary>
    [Test]
    public void InvalidLicensesFallBackToTheDefault()
    {
        var tampered = Encoding.UTF8.GetString(this.File("""{"licensee":"Minh","maxPlayers":5,"expires":null,"machine":null}"""))
            .Replace(Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"licensee":"Minh","maxPlayers":5,"expires":null,"machine":null}""")), Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"licensee":"Minh","maxPlayers":500,"expires":null,"machine":null}""")), StringComparison.Ordinal);
        Assert.That(PlayerLicense.Check(Encoding.UTF8.GetBytes(tampered), this._publicKeyPem, "abc", Today).MaximumPlayers, Is.EqualTo(10), "tampered");
        Assert.That(PlayerLicense.Check(this.File("""{"licensee":"Minh","maxPlayers":50,"expires":"2026-11-14","machine":null}"""), this._publicKeyPem, "abc", Today).MaximumPlayers, Is.EqualTo(10), "expired");
        Assert.That(PlayerLicense.Check(this.File("""{"licensee":"Minh","maxPlayers":50,"expires":null,"machine":"other"}"""), this._publicKeyPem, "abc", Today).MaximumPlayers, Is.EqualTo(10), "other computer");
        Assert.That(PlayerLicense.Check(this.File("""{"licensee":"Minh","maxPlayers":50,"expires":null,"machine":"ABC"}"""), this._publicKeyPem, "abc", Today).MaximumPlayers, Is.EqualTo(50), "this computer");
        Assert.That(PlayerLicense.Check(Encoding.UTF8.GetBytes("not json"), this._publicKeyPem, "abc", Today).MaximumPlayers, Is.EqualTo(10), "garbage");
    }

    /// <summary>
    /// The count is per account. Passing the maximum starts a week of grace (owner, 2026-11-20): more may play then, and
    /// the admin panel asks for a licence; after the week the maximum holds. A new licence ends the grace.
    /// </summary>
    [Test]
    [NonParallelizable]
    public void TheLimitCountsAccountsWithAWeekOfGrace()
    {
        LicensedPlayerLimit.Maximum = 2;
        var previousFile = DeveloperHub.StateFile;
        DeveloperHub.StateFile = null;
        DeveloperHub.ResetMemoryState();
        try
        {
            Assert.That(LicensedPlayerLimit.TryAdmit(null!, "a"), Is.True);
            Assert.That(LicensedPlayerLimit.TryAdmit(null!, "b"), Is.True);
            Assert.That(DeveloperHub.ReadState().GraceStartedUtc, Is.Null, "within the limit");
            Assert.That(LicensedPlayerLimit.TryAdmit(null!, "c"), Is.True, "the third account starts the grace week");
            Assert.That(DeveloperHub.ReadState().GraceStartedUtc, Is.Not.Null);
            Assert.That(LicensedPlayerLimit.TryAdmit(null!, "A"), Is.True, "the same account again");
            Assert.That(LicensedPlayerLimit.Count, Is.EqualTo(3));

            DeveloperHub.ChangeState(s => s.GraceStartedUtc = DateTime.UtcNow.AddDays(-DeveloperHub.GraceDays).AddMinutes(-1));
            Assert.That(LicensedPlayerLimit.TryAdmit(null!, "d"), Is.False, "after the week, the maximum holds");

            DeveloperHub.ClearGrace();
            Assert.That(DeveloperHub.ReadState().GraceStartedUtc, Is.Null, "a licence ends it");
        }
        finally
        {
            LicensedPlayerLimit.Maximum = LicensedPlayerLimit.DefaultMaximum;
            LicensedPlayerLimit.Reset();
            DeveloperHub.ResetMemoryState();
            DeveloperHub.StateFile = previousFile;
        }
    }

    /// <summary>
    /// The real machine id (owner, 2026-11-21) comes from the system's own id, not the computer's name; renaming the
    /// computer keeps it. A licence issued for the old id (the name's hash) is still accepted during the change.
    /// </summary>
    [Test]
    public void TheMachineIdComesFromTheSystemAndOldLicencesStillWork()
    {
        Assert.That(PlayerLicense.MachineIdOf("1b4e28ba-2fa1-11d2-883f-0016d3cca427"), Is.EqualTo(PlayerLicense.MachineIdOf("1B4E28BA2FA111D2883F0016D3CCA427\n")), "the same id however written");
        Assert.That(PlayerLicense.MachineIdOf("1b4e28ba-2fa1-11d2-883f-0016d3cca427"), Has.Length.EqualTo(16));
        Assert.That(PlayerLicense.MachineIdOf("00000000-0000-0000-0000-000000000000"), Is.Null, "no real id");
        Assert.That(PlayerLicense.MachineIdOf(" "), Is.Null);
        Assert.That(PlayerLicense.MachineId, Has.Length.EqualTo(16));
        if (System.IO.File.Exists("/etc/machine-id") && PlayerLicense.MachineIdOf(System.IO.File.ReadAllText("/etc/machine-id")) is { } fromSystem)
        {
            Assert.That(PlayerLicense.MachineId, Is.EqualTo(fromSystem), "Linux: /etc/machine-id");
        }

        var oldLicence = this.File("""{"licensee":"Minh","maxPlayers":50,"expires":null,"machine":"old-name-hash"}""");
        Assert.That(PlayerLicense.Check(oldLicence, this._publicKeyPem, "new-real-id", Today, "old-name-hash").MaximumPlayers, Is.EqualTo(50), "issued for the old id");
        Assert.That(PlayerLicense.Check(oldLicence, this._publicKeyPem, "new-real-id", Today, "another-name").MaximumPlayers, Is.EqualTo(10), "another computer");
        Assert.That(PlayerLicense.Check(oldLicence, this._publicKeyPem, "new-real-id", Today).MaximumPlayers, Is.EqualTo(10), "no old id given");
    }

    /// <summary>
    /// The first-setup report goes once to the hub's /api/setup with the system, the game, the host's name and contact
    /// and both machine ids; a refusal leaves it to be tried again.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task TheFirstSetupReportIsSentOnceWithTheHostsDetailsAsync()
    {
        var previousFile = DeveloperHub.StateFile;
        var (previousMachine, previousLegacy) = (DeveloperHub.MachineId, DeveloperHub.LegacyMachineId);
        DeveloperHub.StateFile = null;
        DeveloperHub.ResetMemoryState();
        DeveloperHub.MachineId = "aaaaaaaaaaaaaaaa";
        DeveloperHub.LegacyMachineId = "bbbbbbbbbbbbbbbb";
        try
        {
            // The host's name and contact come silently from a licence request (owner, 2026-11-21).
            using (var requestClient = new HttpClient(new RecordingHandler(HttpStatusCode.OK, """{"request":"R-1"}""")))
            {
                await DeveloperHub.RequestLicenceAsync(" Minh ", "zalo 0900", 20, "hello", requestClient).ConfigureAwait(false);
            }

            Assert.That((DeveloperHub.ReadState().HostName, DeveloperHub.ReadState().HostContact), Is.EqualTo(("Minh", "zalo 0900")));
            var refuse = new RecordingHandler(HttpStatusCode.TooManyRequests);
            using (var client = new HttpClient(refuse))
            {
                var (ok, _) = await DeveloperHub.SendSetupAsync(new JsonObject { ["system"] = new JsonObject { ["cores"] = 8 } }, client).ConfigureAwait(false);
                Assert.That(ok, Is.False);
            }

            Assert.That(DeveloperHub.ReadState().SetupReportedUtc, Is.Null, "refused: tried again later");
            Assert.That(DeveloperHub.ReadState().SetupTriedUtc, Is.Not.Null);

            var take = new RecordingHandler(HttpStatusCode.OK);
            using (var client = new HttpClient(take))
            {
                var (ok, _) = await DeveloperHub.SendSetupAsync(new JsonObject { ["system"] = new JsonObject { ["cores"] = 8 } }, client).ConfigureAwait(false);
                Assert.That(ok, Is.True);
            }

            Assert.That(DeveloperHub.ReadState().SetupReportedUtc, Is.Not.Null);
            Assert.That(take.Path, Is.EqualTo("/api/setup"));
            var sent = JsonNode.Parse(take.Body!)!;
            Assert.That(sent["machine"]!.GetValue<string>(), Is.EqualTo("aaaaaaaaaaaaaaaa"));
            Assert.That(sent["legacyMachine"]!.GetValue<string>(), Is.EqualTo("bbbbbbbbbbbbbbbb"));
            Assert.That(sent["host"]!["name"]!.GetValue<string>(), Is.EqualTo("Minh"));
            Assert.That(sent["host"]!["contact"]!.GetValue<string>(), Is.EqualTo("zalo 0900"));
            Assert.That(sent["system"]!["cores"]!.GetValue<int>(), Is.EqualTo(8));
            Assert.That(sent["test"], Is.Null, "a real host says nothing about tests");

            // One of the developer's test machines says so (HNMO_TEST_HOST=1); the console keeps it apart.
            Environment.SetEnvironmentVariable("HNMO_TEST_HOST", "1");
            var weekly = new RecordingHandler(HttpStatusCode.OK);
            using (var client = new HttpClient(weekly))
            {
                await DeveloperHub.SendWeeklyAsync(new JsonObject { ["game"] = new JsonObject { ["name"] = "Mu Rồng Vàng" } }, client).ConfigureAwait(false);
            }

            Assert.That(JsonNode.Parse(weekly.Body!)!["test"]!.GetValue<bool>(), Is.True);
        }
        finally
        {
            DeveloperHub.ResetMemoryState();
            DeveloperHub.StateFile = previousFile;
            (DeveloperHub.MachineId, DeveloperHub.LegacyMachineId) = (previousMachine, previousLegacy);
            Environment.SetEnvironmentVariable("HNMO_TEST_HOST", null);
        }
    }

    /// <summary>
    /// Layer 1: update checks ask <c>/api/update-check</c> with the folder's licence, machine id and build stamp;
    /// valid returns the manifest and token, withdrawn (403) refuses cleanly, and unreachable never crashes.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    [NonParallelizable]
    public async Task UpdateCheckRespectsValidWithdrawnAndUnreachableHubAsync()
    {
        var (previousMachine, previousLegacy, previousLicence) = (DeveloperHub.MachineId, DeveloperHub.LegacyMachineId, DeveloperHub.LicenceFile);
        DeveloperHub.MachineId = "aaaaaaaaaaaaaaaa";
        DeveloperHub.LegacyMachineId = "bbbbbbbbbbbbbbbb";
        DeveloperHub.LicenceFile = Encoding.UTF8.GetString(this.File("""{"licensee":"Minh","maxPlayers":40,"expires":null,"machine":"aaaaaaaaaaaaaaaa","id":"L-1"}"""));
        try
        {
            var validJson = """
            {
              "manifestVersion": "1.2.0",
              "notes": "Patch",
              "downloadBase": "https://cdn.example/files/",
              "token": "tok-abc",
              "files": [{"path": "server/HNMO.Startup.dll", "sha256": "deadbeef", "size": 42, "exec": false}]
            }
            """;
            var validHandler = new RecordingHandler(HttpStatusCode.OK, validJson);
            using (var client = new HttpClient(validHandler))
            {
                var res = await DeveloperHub.CheckForUpdateAsync("HNMO-test-20261127", client: client).ConfigureAwait(false);
                Assert.That(res.Authorized, Is.True);
                Assert.That(res.ManifestVersion, Is.EqualTo("1.2.0"));
                Assert.That(res.Token, Is.EqualTo("tok-abc"));
                Assert.That(res.DownloadBase, Is.EqualTo("https://cdn.example/files/"));
                Assert.That(res.Files, Has.Count.EqualTo(1));
                Assert.That(validHandler.Path, Is.EqualTo("/api/update-check"));
                var sent = JsonNode.Parse(validHandler.Body!)!;
                Assert.That(sent["licenceId"]!.GetValue<string>(), Is.EqualTo("L-1"));
                Assert.That(sent["machineId"]!.GetValue<string>(), Is.EqualTo("aaaaaaaaaaaaaaaa"));
                Assert.That(sent["buildStamp"]!.GetValue<string>(), Is.EqualTo("HNMO-test-20261127"));
                Assert.That(Encoding.UTF8.GetString(Convert.FromBase64String(sent["licence"]!.GetValue<string>())), Is.EqualTo(DeveloperHub.LicenceFile));
            }

            using (var withdrawnClient = new HttpClient(new RecordingHandler(HttpStatusCode.Forbidden, """{"error":"forbidden","reason":"withdrawn"}""")))
            {
                var res = await DeveloperHub.CheckForUpdateAsync("HNMO-test-20261127", client: withdrawnClient).ConfigureAwait(false);
                Assert.That(res.Authorized, Is.False);
                Assert.That(res.Denied, Is.True);
                Assert.That(res.Files, Is.Empty);
            }

            using (var unreachableClient = new HttpClient(new ThrowingHandler()))
            {
                var res = await DeveloperHub.CheckForUpdateAsync("HNMO-test-20261127", client: unreachableClient).ConfigureAwait(false);
                Assert.That(res.Authorized, Is.False);
                Assert.That(res.Denied, Is.False);
                Assert.That(res.Files, Is.Empty);
            }

            // No licence: the folder asks as a free host, without a licence in the request.
            var freeHandler = new RecordingHandler(HttpStatusCode.OK, validJson);
            using (var freeClient = new HttpClient(freeHandler))
            {
                var res = await DeveloperHub.CheckForUpdateAsync("HNMO-test-20261127", licenceFile: string.Empty, client: freeClient).ConfigureAwait(false);
                Assert.That(res.Authorized, Is.True);
                var sent = JsonNode.Parse(freeHandler.Body!)!.AsObject();
                Assert.That(sent["machineId"]!.GetValue<string>(), Is.EqualTo("aaaaaaaaaaaaaaaa"));
                Assert.That(sent.ContainsKey("licence"), Is.False);
                Assert.That(sent.ContainsKey("licenceId"), Is.False);
            }
        }
        finally
        {
            (DeveloperHub.MachineId, DeveloperHub.LegacyMachineId, DeveloperHub.LicenceFile) = (previousMachine, previousLegacy, previousLicence);
        }
    }

    /// <summary>
    /// One limit for the whole folder (owner, 2026-11-21): accounts playing in another program (Season 9, the Arena)
    /// take places too, an account playing elsewhere coming here takes none, and this program publishes its accounts.
    /// </summary>
    [Test]
    [NonParallelizable]
    public void TheLimitCountsEveryProgramTogether()
    {
        var previousShared = ArenaShared.Current;
        var previousFile = DeveloperHub.StateFile;
        var shared = new LimitShared { Elsewhere = ["s9player", "arenaplayer"] };
        ArenaShared.Current = shared;
        DeveloperHub.StateFile = null;
        DeveloperHub.ResetMemoryState();
        DeveloperHub.ChangeState(s => s.GraceStartedUtc = DateTime.UtcNow.AddDays(-DeveloperHub.GraceDays - 1));
        LicensedPlayerLimit.Reset();
        LicensedPlayerLimit.Maximum = 3;
        try
        {
            Assert.That(LicensedPlayerLimit.TryAdmit(null!, "home"), Is.True, "2 elsewhere + 1 here = 3");
            Assert.That(LicensedPlayerLimit.TotalCount, Is.EqualTo(3));
            Assert.That(LicensedPlayerLimit.TryAdmit(null!, "fourth"), Is.False, "the folder is full");
            Assert.That(LicensedPlayerLimit.TryAdmit(null!, "ARENAPLAYER"), Is.True, "an account playing elsewhere takes no new place");
            Assert.That(LicensedPlayerLimit.TotalCount, Is.EqualTo(3));
            Assert.That(() => shared.Published.Any(p => p.Contains("home")), Is.True.After(5000, 20), "this program publishes its accounts");
        }
        finally
        {
            ArenaShared.Current = previousShared;
            LicensedPlayerLimit.Maximum = LicensedPlayerLimit.DefaultMaximum;
            LicensedPlayerLimit.Reset();
            DeveloperHub.ResetMemoryState();
            DeveloperHub.StateFile = previousFile;
        }
    }

    /// <summary>
    /// Any tampered binary integrity status silently downgrades the limit to 10 across Startup, Login, and Weekly ticks without throwing.
    /// </summary>
    /// <param name="tamperedStatus">The integrity status representing tampering.</param>
    [TestCase(IntegrityStatus.FileMismatch)]
    [TestCase(IntegrityStatus.SignatureInvalid)]
    [TestCase(IntegrityStatus.ManifestInvalid)]
    [NonParallelizable]
    public async Task TamperedIntegritySilentlyDowngradesAcrossCheckpointsAsync(IntegrityStatus tamperedStatus)
    {
        var previousFile = DeveloperHub.StateFile;
        var previousLicence = DeveloperHub.LicenceFile;
        var (previousMachine, previousLegacy) = (DeveloperHub.MachineId, DeveloperHub.LegacyMachineId);
        DeveloperHub.StateFile = null;
        DeveloperHub.ResetMemoryState();
        DeveloperHub.MachineId = "m1";
        DeveloperHub.LegacyMachineId = string.Empty;
        DeveloperHub.ChangeState(s => s.GraceStartedUtc = DateTime.UtcNow.AddDays(-DeveloperHub.GraceDays - 1));
        LicensedPlayerLimit.Reset();
        try
        {
            var bytes = this.File("""{"id":"lic-1","licensee":"Minh","maxPlayers":50,"expires":"2027-01-31","machine":"m1"}""");
            var status = IntegrityStatus.Ok;
            LicensedPlayerLimit.ConfigureBuildIntegrity(
                () => status,
                (b, d) => PlayerLicense.Check(b, this._publicKeyPem, "m1", Today));

            Assert.That(LicensedPlayerLimit.ApplyLicence(bytes, new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc)), Is.EqualTo(50));

            status = tamperedStatus;
            Assert.That(LicensedPlayerLimit.ReevaluateAndEnforce(new DateTime(2026, 11, 15, 0, 1, 0, DateTimeKind.Utc)), Is.EqualTo(10));
            Assert.That(LicensedPlayerLimit.Maximum, Is.EqualTo(10));
            Assert.That(LicensedPlayerLimit.Licensee, Is.Null);

            for (var i = 0; i < 10; i++)
            {
                Assert.That(LicensedPlayerLimit.TryAdmit(null, $"player{i}"), Is.True);
            }

            Assert.That(LicensedPlayerLimit.TryAdmit(null, "player10"), Is.False);

            var handler = new RecordingHandler(HttpStatusCode.OK);
            var weeklyService = CreateWeeklyService();
            using var client = new HttpClient(handler);
            await weeklyService.SendWeeklyAsync(null, client).ConfigureAwait(false);
            var parsed = JsonNode.Parse(handler.Body!)!;
            Assert.That(parsed["players"]!["limit"]!.GetValue<int>(), Is.EqualTo(10));
            Assert.That(parsed["buildStamp"]!.GetValue<string>(), Is.EqualTo(HnmoBuildStamp.Resolve()));
        }
        finally
        {
            LicensedPlayerLimit.ResetVerification();
            LicensedPlayerLimit.Reset();
            DeveloperHub.LicenceFile = previousLicence;
            (DeveloperHub.MachineId, DeveloperHub.LegacyMachineId) = (previousMachine, previousLegacy);
            DeveloperHub.ResetMemoryState();
            DeveloperHub.StateFile = previousFile;
        }
    }

    /// <summary>
    /// Invalid, expired, wrong-machine, or hub-withdrawn licences silently enforce the default 10-player cap.
    /// </summary>
    [Test]
    [NonParallelizable]
    public void InvalidOrWithdrawnLicenceSilentlyDowngradesToDefault()
    {
        var previousFile = DeveloperHub.StateFile;
        var previousLicence = DeveloperHub.LicenceFile;
        DeveloperHub.StateFile = null;
        DeveloperHub.ResetMemoryState();
        LicensedPlayerLimit.Reset();
        try
        {
            LicensedPlayerLimit.ConfigureBuildIntegrity(
                () => IntegrityStatus.Ok,
                (b, d) => PlayerLicense.Check(b, this._publicKeyPem, "m1", d));

            var wrongMachine = this.File("""{"id":"lic-wrong","licensee":"Minh","maxPlayers":50,"expires":"2027-01-31","machine":"other"}""");
            Assert.That(LicensedPlayerLimit.ApplyLicence(wrongMachine, new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc)), Is.EqualTo(10));

            var expired = this.File("""{"id":"lic-exp","licensee":"Minh","maxPlayers":50,"expires":"2026-11-10","machine":"m1"}""");
            Assert.That(LicensedPlayerLimit.ApplyLicence(expired, new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc)), Is.EqualTo(10));

            var withdrawn = this.File("""{"id":"lic-rev","licensee":"Minh","maxPlayers":50,"expires":"2027-01-31","machine":"m1"}""");
            Assert.That(LicensedPlayerLimit.ApplyLicence(withdrawn, new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc)), Is.EqualTo(50));

            DeveloperHub.ChangeState(s => s.WithdrawnLicences.Add("lic-rev"));
            Assert.That(LicensedPlayerLimit.ReevaluateAndEnforce(new DateTime(2026, 11, 15, 1, 0, 0, DateTimeKind.Utc)), Is.EqualTo(10));
            Assert.That(LicensedPlayerLimit.Licensee, Is.Null);
        }
        finally
        {
            LicensedPlayerLimit.ResetVerification();
            LicensedPlayerLimit.Reset();
            DeveloperHub.LicenceFile = previousLicence;
            DeveloperHub.ResetMemoryState();
            DeveloperHub.StateFile = previousFile;
        }
    }

    /// <summary>
    /// Hub unreachability past <see cref="DeveloperHub.GraceDays"/> silently downgrades the cap to 10 and clears once reachable again.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    [NonParallelizable]
    public async Task UnreachableHubPastGraceDaysSilentlyDowngradesAsync()
    {
        var previousFile = DeveloperHub.StateFile;
        var previousLicence = DeveloperHub.LicenceFile;
        var (previousMachine, previousLegacy) = (DeveloperHub.MachineId, DeveloperHub.LegacyMachineId);
        DeveloperHub.StateFile = null;
        DeveloperHub.ResetMemoryState();
        DeveloperHub.MachineId = "m1";
        DeveloperHub.LegacyMachineId = string.Empty;
        LicensedPlayerLimit.Reset();
        try
        {
            var validBytes = this.File("""{"id":"lic-net","licensee":"Minh","maxPlayers":50,"expires":"2027-01-31","machine":"m1"}""");
            DeveloperHub.LicenceFile = Encoding.UTF8.GetString(validBytes);
            LicensedPlayerLimit.ConfigureBuildIntegrity(
                () => IntegrityStatus.Ok,
                (b, _) => PlayerLicense.Check(b, this._publicKeyPem, "m1", Today));
            Assert.That(LicensedPlayerLimit.ApplyLicence(validBytes, new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc)), Is.EqualTo(50));

            using var failingClient = new HttpClient(new ThrowingHandler());
            var (ok, _, _) = await DeveloperHub.SendWeeklyAsync(new JsonObject(), failingClient).ConfigureAwait(false);
            Assert.That(ok, Is.False);
            var recorded = DeveloperHub.ReadState().FirstHubUnreachableUtc;
            Assert.That(recorded, Is.Not.Null);

            Assert.That(LicensedPlayerLimit.ReevaluateAndEnforce(recorded!.Value.AddDays(DeveloperHub.GraceDays).AddMinutes(-1)), Is.EqualTo(50));
            Assert.That(LicensedPlayerLimit.ReevaluateAndEnforce(recorded.Value.AddDays(DeveloperHub.GraceDays)), Is.EqualTo(10));

            using var okClient = new HttpClient(new RecordingHandler(HttpStatusCode.OK));
            var (recovered, _, _) = await DeveloperHub.SendWeeklyAsync(new JsonObject(), okClient).ConfigureAwait(false);
            Assert.That(recovered, Is.True);
            Assert.That(DeveloperHub.ReadState().FirstHubUnreachableUtc, Is.Null);
        }
        finally
        {
            LicensedPlayerLimit.ResetVerification();
            LicensedPlayerLimit.Reset();
            DeveloperHub.LicenceFile = previousLicence;
            (DeveloperHub.MachineId, DeveloperHub.LegacyMachineId) = (previousMachine, previousLegacy);
            DeveloperHub.ResetMemoryState();
            DeveloperHub.StateFile = previousFile;
        }
    }

    /// <summary>
    /// Memory patching <see cref="LicensedPlayerLimit.Maximum"/> cannot raise the limit past the re-derived cap.
    /// </summary>
    [Test]
    [NonParallelizable]
    public void MemoryPatchedMaximumCannotBypassDerivedLimit()
    {
        var previousFile = DeveloperHub.StateFile;
        DeveloperHub.StateFile = null;
        DeveloperHub.ResetMemoryState();
        LicensedPlayerLimit.Reset();
        try
        {
            LicensedPlayerLimit.Maximum = 500;
            Assert.That(LicensedPlayerLimit.Maximum, Is.EqualTo(10), "without verification context, patch above default is rejected");

            var bytes = this.File("""{"id":"lic-cap","licensee":"Minh","maxPlayers":25,"expires":"2027-01-31","machine":"m1"}""");
            LicensedPlayerLimit.ConfigureBuildIntegrity(
                () => IntegrityStatus.Ok,
                (b, _) => PlayerLicense.Check(b, this._publicKeyPem, "m1", Today));
            Assert.That(LicensedPlayerLimit.ApplyLicence(bytes, new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc)), Is.EqualTo(25));

            LicensedPlayerLimit.Maximum = 999;
            Assert.That(LicensedPlayerLimit.Maximum, Is.EqualTo(25));
            Assert.That(LicensedPlayerLimit.ReevaluateAndEnforce(new DateTime(2026, 11, 15, 0, 5, 0, DateTimeKind.Utc)), Is.EqualTo(25));
        }
        finally
        {
            LicensedPlayerLimit.ResetVerification();
            LicensedPlayerLimit.Reset();
            DeveloperHub.ResetMemoryState();
            DeveloperHub.StateFile = previousFile;
        }
    }

    private static DeveloperHubWeeklyService CreateWeeklyService()
    {
        var servers = new Mock<IServerProvider>();
        servers.SetupGet(s => s.Servers).Returns(Array.Empty<IManageableServer>());
        var licences = new Mock<IHostLicenseProvider>();
        return new DeveloperHubWeeklyService(servers.Object, licences.Object, NullLogger<DeveloperHubWeeklyService>.Instance);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("simulated outage");
    }

    private byte[] File(string payloadJson)
    {
        var payload = Encoding.UTF8.GetBytes(payloadJson);
        var signer = new Ed25519Signer();
        signer.Init(true, this._privateKey);
        signer.BlockUpdate(payload, 0, payload.Length);
        var signature = signer.GenerateSignature();
        return Encoding.UTF8.GetBytes($$"""{"payload":"{{Convert.ToBase64String(payload)}}","signature":"{{Convert.ToBase64String(signature)}}"}""");
    }

    private sealed class LimitShared : IArenaShared
    {
        public IReadOnlyList<string> Elsewhere { get; init; } = [];

        public System.Collections.Concurrent.ConcurrentBag<string[]> Published { get; } = [];

        public int AddPoints(string login, int points, int dailyCap, string dayKey) => points;

        public bool TrySpendPoints(string login, int points) => false;

        public void RefundPoints(string login, int points)
        {
        }

        public int GetPoints(string login) => 0;

        public void SetRating(string login, int rating)
        {
        }

        public int? GetRating(string login) => null;

        public IReadOnlyList<string> LicensedElsewhere(string season) => this.Elsewhere;

        public void PublishLicensed(string season, IReadOnlyCollection<string> logins) => this.Published.Add(logins.ToArray());
    }

    private sealed class RecordingHandler(HttpStatusCode status, string answer = "{\"ok\":true}") : HttpMessageHandler
    {
        public string? Path { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.Path = request.RequestUri?.AbsolutePath;
            this.Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(status) { Content = new StringContent(answer) };
        }
    }
}
