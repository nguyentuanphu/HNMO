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
using HNMO.Persistence;
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
    }

    /// <summary>No file: the default of 10.</summary>
    [Test]
    public void WithoutLicenseTheDefaultApplies()
    {
        var license = PlayerLicense.Check(null, this._publicKeyPem, "abc", Today);
        Assert.That(license.IsValid, Is.False);
        Assert.That(license.MaximumPlayers, Is.EqualTo(10));
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
            DeveloperHub.ChangeState(s => (s.HostName, s.HostContact) = ("Minh", "zalo 0900"));
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
        }
        finally
        {
            DeveloperHub.ResetMemoryState();
            DeveloperHub.StateFile = previousFile;
            (DeveloperHub.MachineId, DeveloperHub.LegacyMachineId) = (previousMachine, previousLegacy);
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

    private sealed class RecordingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public string? Path { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.Path = request.RequestUri?.AbsolutePath;
            this.Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(status) { Content = new StringContent("{\"ok\":true}") };
        }
    }
}
