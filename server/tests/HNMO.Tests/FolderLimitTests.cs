// <copyright file="FolderLimitTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Threading;
using HNMO.GameLogic;
using HNMO.Integrity;
using HNMO.Interfaces;
using NUnit.Framework;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;

/// <summary>
/// The licence's folder limit: the folder code sent to the hub, the licence's <c>folders</c>, the seats of a computer,
/// and the hub's switch (off by default, relaxed when the hub can't be reached).
/// </summary>
[TestFixture]
[NonParallelizable]
public class FolderLimitTests
{
    private static readonly DateOnly Today = new(2026, 11, 28);
    private DirectoryInfo _dir = null!;
    private string? _previousHub;
    private string? _previousState;
    private string _previousMachine = string.Empty;
    private string _previousFolder = string.Empty;
    private DateTime _now;

    /// <summary>A fresh directory, a stub hub address and the hub's state in memory.</summary>
    [SetUp]
    public void SetUp()
    {
        this._dir = Directory.CreateTempSubdirectory("hnmo-folders-");
        this._previousHub = Environment.GetEnvironmentVariable(DeveloperHubAddress.EnvironmentVariableName);
        Environment.SetEnvironmentVariable(DeveloperHubAddress.EnvironmentVariableName, "https://hub.example");
        this._previousState = DeveloperHub.StateFile;
        this._previousMachine = DeveloperHub.MachineId;
        this._previousFolder = DeveloperHub.FolderCode;
        DeveloperHub.StateFile = null;
        DeveloperHub.ResetMemoryState();
        DeveloperHub.MachineId = "machine1";
        LicensedPlayerLimit.Reset();
        LicensedPlayerLimit.ResetVerification();
        FolderSeat.Reset();
        this._now = new DateTime(2026, 11, 28, 1, 0, 0, DateTimeKind.Utc);
    }

    /// <summary>Puts everything back.</summary>
    [TearDown]
    public void TearDown()
    {
        FolderSeat.Reset();
        LicensedPlayerLimit.Reset();
        LicensedPlayerLimit.ResetVerification();
        DeveloperHub.StateFile = this._previousState;
        DeveloperHub.ResetMemoryState();
        DeveloperHub.MachineId = this._previousMachine;
        DeveloperHub.FolderCode = this._previousFolder;
        Environment.SetEnvironmentVariable(DeveloperHubAddress.EnvironmentVariableName, this._previousHub);
        this._dir.Delete(recursive: true);
    }

    /// <summary>(a) The code: the first 16 hex digits of SHA-256 of "installId|path".</summary>
    [Test]
    public void TheFolderCodeIsTheHashOfInstallIdAndPath()
    {
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("0123456789abcdef|/srv/hnmo")))[..16].ToLowerInvariant();
        Assert.That(FolderIdentity.CodeOf("0123456789abcdef", "/srv/hnmo"), Is.EqualTo(expected));
        Assert.That(expected, Does.Match("^[0-9a-f]{16}$"));
        Assert.That(FolderIdentity.CodeOf("0123456789abcdef", "/srv/hnmo2"), Is.Not.EqualTo(expected), "another path");
        Assert.That(FolderIdentity.CodeOf("fedcba9876543210", "/srv/hnmo"), Is.Not.EqualTo(expected), "another install");
    }

    /// <summary>
    /// (a) The install id is made once in save/ and kept: every program of the folder gets the same code, a copy of the
    /// folder elsewhere another one.
    /// </summary>
    [Test]
    public void TheInstallIdIsMadeOnceAndACopyGetsAnotherCode()
    {
        var save = Path.Combine(this._dir.FullName, "HNMO", "save");
        var seasonSix = FolderIdentity.CodeForSaveDirectory(save);
        var seasonNine = FolderIdentity.CodeForSaveDirectory(save);
        Assert.That(seasonNine, Is.EqualTo(seasonSix), "programs of one folder");
        Assert.That(File.Exists(Path.Combine(save, FolderIdentity.InstallIdFileName)), Is.True);
        Assert.That(File.ReadAllText(Path.Combine(save, FolderIdentity.InstallIdFileName)), Does.Match("^[0-9a-f]{32}$"));

        var copy = Path.Combine(this._dir.FullName, "HNMO copy", "save");
        Directory.CreateDirectory(copy);
        File.Copy(Path.Combine(save, FolderIdentity.InstallIdFileName), Path.Combine(copy, FolderIdentity.InstallIdFileName));
        Assert.That(FolderIdentity.CodeForSaveDirectory(copy), Is.Not.EqualTo(seasonSix), "the copied folder");
        Assert.That(FolderIdentity.ReadOrCreateInstallId(copy), Is.EqualTo(FolderIdentity.ReadOrCreateInstallId(save)), "the id itself is copied");
    }

    /// <summary>(a) The weekly and the setup report carry the folder code as <c>folder</c>.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task TheReportsCarryTheFolderCodeAsync()
    {
        DeveloperHub.FolderCode = "0011223344556677";
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var client = new HttpClient(handler);
        await DeveloperHub.SendWeeklyAsync(new JsonObject(), client).ConfigureAwait(false);
        Assert.That(JsonNode.Parse(handler.Body!)!["folder"]!.GetValue<string>(), Is.EqualTo("0011223344556677"), "weekly");
        await DeveloperHub.SendSetupAsync(new JsonObject(), client).ConfigureAwait(false);
        Assert.That(handler.Path, Is.EqualTo("/api/setup"));
        Assert.That(JsonNode.Parse(handler.Body!)!["folder"]!.GetValue<string>(), Is.EqualTo("0011223344556677"), "setup");
    }

    /// <summary>(b) <c>folders</c>: 1 for older licences without it, the signed value within 1..50 otherwise.</summary>
    /// <param name="field">The field (JSON), or null for none.</param>
    /// <param name="expected">The folders read.</param>
    [TestCase(null, 1)]
    [TestCase("3", 3)]
    [TestCase("50", 50)]
    [TestCase("99", 50)]
    [TestCase("0", 1)]
    [TestCase("\"5\"", 1)]
    public void FoldersOfALicence(string? field, int expected)
    {
        var (key, pem) = NewKey();
        var extra = field is null ? string.Empty : $",\"folders\":{field}";
        var license = PlayerLicense.Check(Signed(key, $$"""{"licensee":"Minh","maxPlayers":50,"expires":null,"machine":null{{extra}}}"""), pem, "machine1", Today);
        Assert.That(license.IsValid, Is.True);
        Assert.That(license.Folders, Is.EqualTo(expected));
        Assert.That(license.MaximumPlayers, Is.EqualTo(50));
    }

    /// <summary>(b) <c>folders</c> is signed: a changed value breaks the licence; without a licence one folder.</summary>
    [Test]
    public void FoldersIsSigned()
    {
        var (key, pem) = NewKey();
        const string payload = """{"licensee":"Minh","maxPlayers":50,"expires":null,"machine":null,"folders":1}""";
        var tampered = Encoding.UTF8.GetString(Signed(key, payload)).Replace(
            Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)),
            Convert.ToBase64String(Encoding.UTF8.GetBytes(payload.Replace("\"folders\":1", "\"folders\":9", StringComparison.Ordinal))),
            StringComparison.Ordinal);
        var license = PlayerLicense.Check(Encoding.UTF8.GetBytes(tampered), pem, "machine1", Today);
        Assert.That(license.IsValid, Is.False);
        Assert.That(license.Folders, Is.EqualTo(1));
        Assert.That(PlayerLicense.Check(null, pem, "machine1", Today).Folders, Is.EqualTo(1));
    }

    /// <summary>(b) The folders allowed come from a valid licence, and are 1 without one (the free build).</summary>
    [Test]
    public void FoldersAllowedFollowTheLicence()
    {
        Assert.That(LicensedPlayerLimit.FoldersAllowed(this._now), Is.EqualTo(1), "free build");
        LicensedPlayerLimit.ConfigureBuildIntegrity(() => IntegrityStatus.Ok, (_, _) => new PlayerLicense(true, 50, "Minh", null, "ok", null, 3));
        LicensedPlayerLimit.ApplyLicence([1], this._now);
        Assert.That(LicensedPlayerLimit.FoldersAllowed(this._now), Is.EqualTo(3));
        LicensedPlayerLimit.ConfigureBuildIntegrity(() => IntegrityStatus.Ok, (_, _) => PlayerLicense.Default("expired") with { Folders = 7 });
        Assert.That(LicensedPlayerLimit.FoldersAllowed(this._now), Is.EqualTo(1), "an invalid licence's folders don't count");
    }

    /// <summary>
    /// (c) Two programs of one folder share one seat, a second folder is refused (until the licence allows two), and a
    /// holder which stopped beating frees its seat after the expiry.
    /// </summary>
    [Test]
    public void SeatsAreSharedRefusedAndReleased()
    {
        var seatDir = Path.Combine(this._dir.FullName, "seats");
        var seasonSix = this.Seats(seatDir, "aaaaaaaaaaaaaaaa", "/srv/A");
        var seasonNine = this.Seats(seatDir, "aaaaaaaaaaaaaaaa", "/srv/A");
        var other = this.Seats(seatDir, "bbbbbbbbbbbbbbbb", "/srv/B");

        Assert.That(seasonSix.Refresh(1).HasSeat, Is.True, "first folder");
        this._now = this._now.AddSeconds(5);
        Assert.That(seasonNine.Refresh(1).HasSeat, Is.True, "same folder, another program");
        Assert.That(Directory.GetFiles(seatDir, "*.seat"), Has.Length.EqualTo(1), "one seat for the folder");

        var refused = other.Refresh(1);
        Assert.That(refused.HasSeat, Is.False, "second folder");
        Assert.That(refused.OtherHolders, Is.EqualTo(new[] { "/srv/A" }));
        Assert.That(other.Refresh(2).HasSeat, Is.True, "a licence for two folders");
        Assert.That(seasonSix.Refresh(1).HasSeat, Is.True, "the first folder keeps its seat while the second waits");

        // Folder A's programs stop (no heartbeat); after the expiry the waiting folder gets the seat.
        this._now = this._now.AddSeconds(60);
        Assert.That(other.Refresh(1).HasSeat, Is.False, "A beat a minute ago");
        this._now = this._now.AddSeconds(70);
        Assert.That(other.Refresh(1).HasSeat, Is.True, "A's seat expired");
        Assert.That(File.Exists(seasonSix.OwnFile), Is.False, "the dead seat file is removed");
        Assert.That(seasonSix.Refresh(1).HasSeat, Is.False, "A comes back and now waits behind B");
    }

    /// <summary>(c) Seats are per computer: another machine id's file in the same directory doesn't count.</summary>
    [Test]
    public void SeatsArePerComputer()
    {
        var seatDir = Path.Combine(this._dir.FullName, "seats");
        Assert.That(new FolderSeats(seatDir, "othermachine", "aaaaaaaaaaaaaaaa", "/srv/A", () => this._now).Refresh(1).HasSeat, Is.True);
        Assert.That(this.Seats(seatDir, "bbbbbbbbbbbbbbbb", "/srv/B").Refresh(1).HasSeat, Is.True);
    }

    /// <summary>
    /// (c) With the hub's switch on, a folder without a seat allows 0 players and refuses logins; once the seat is free,
    /// or the switch is off, it takes players again without a restart.
    /// </summary>
    [Test]
    public void AFolderWithoutASeatTakesNobodyUntilOneIsFree()
    {
        var seatDir = Path.Combine(this._dir.FullName, "seats");
        var holder = this.Seats(seatDir, "aaaaaaaaaaaaaaaa", "/srv/A");
        holder.Refresh(1);
        DeveloperHub.ChangeState(s => s.FolderLimits = new HubFolderLimits { Enforce = true, ReceivedUtc = this._now });
        FolderSeat.Configure(this.Seats(seatDir, "bbbbbbbbbbbbbbbb", "/srv/B"), null);

        var status = FolderSeat.Evaluate(this._now);
        Assert.That(status.IsBlocked, Is.True);
        Assert.That(status.MachineId, Is.EqualTo("machine1"));
        Assert.That(status.OtherHolders, Is.EqualTo(new[] { "/srv/A" }));
        Assert.That(LicensedPlayerLimit.Maximum, Is.EqualTo(0));
        Assert.That(LicensedPlayerLimit.ReevaluateAndEnforce(this._now), Is.EqualTo(0));
        Assert.That(LicensedPlayerLimit.TryAdmit(null, "test0"), Is.False);

        this._now = this._now.AddMinutes(3);
        Assert.That(FolderSeat.Evaluate(this._now).IsBlocked, Is.False, "the holder stopped beating");
        Assert.That(LicensedPlayerLimit.Maximum, Is.EqualTo(LicensedPlayerLimit.DefaultMaximum));
        Assert.That(LicensedPlayerLimit.TryAdmit(null, "test0"), Is.True);

        // The holder is back and first in line again only after B gave up; the switch going off frees B at once.
        DeveloperHub.ChangeState(s => s.FolderLimits = new HubFolderLimits { Enforce = false, ReceivedUtc = this._now });
        Assert.That(FolderSeat.Evaluate(this._now), Is.EqualTo(FolderSeatStatus.Off));
        Assert.That(Directory.GetFiles(seatDir, "*.seat"), Is.Empty, "the seat is given up when the switch goes off");
    }

    /// <summary>(d) With the switch off (the default), nothing happens: no seat file, no block, the limit as before.</summary>
    [Test]
    public void WithTheSwitchOffNothingChanges()
    {
        var seatDir = Path.Combine(this._dir.FullName, "seats");
        FolderSeat.Configure(this.Seats(seatDir, "bbbbbbbbbbbbbbbb", "/srv/B"), null);
        Assert.That(FolderSeat.Evaluate(this._now), Is.EqualTo(FolderSeatStatus.Off), "no answer yet");
        DeveloperHub.ChangeState(s => s.FolderLimits = new HubFolderLimits { Enforce = false, ReceivedUtc = this._now });
        Assert.That(FolderSeat.Evaluate(this._now).IsBlocked, Is.False, "switched off");
        Assert.That(Directory.Exists(seatDir), Is.False, "no seat directory, no file");
        Assert.That(LicensedPlayerLimit.Maximum, Is.EqualTo(LicensedPlayerLimit.DefaultMaximum));
        Assert.That(LicensedPlayerLimit.TryAdmit(null, "test0"), Is.True);
    }

    /// <summary>(d, e) The hub's answers set the switch: missing means off; it lasts 14 days; an outage relaxes it.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task TheHubsAnswersSetTheSwitchAndAnOutageRelaxesItAsync()
    {
        using (var on = new HttpClient(new RecordingHandler(HttpStatusCode.OK, """{"ok":true,"limits":{"enforce":true}}""")))
        {
            await DeveloperHub.SendWeeklyAsync(new JsonObject(), on).ConfigureAwait(false);
        }

        var received = DeveloperHub.ReadState().FolderLimits!.ReceivedUtc;
        Assert.That(DeveloperHub.FolderLimitEnforced(received), Is.True, "weekly answer with enforce");
        Assert.That(DeveloperHub.FolderLimitEnforced(received.AddDays(13)), Is.True, "13 days later");
        Assert.That(DeveloperHub.FolderLimitEnforced(received.AddDays(15)), Is.False, "older than 14 days");

        // The hub can't be reached: the last answer is kept, but a week of outage relaxes the limit.
        using (var down = new HttpClient(new ThrowingHandler()))
        {
            await DeveloperHub.SendWeeklyAsync(new JsonObject(), down).ConfigureAwait(false);
        }

        var outage = DeveloperHub.ReadState().FirstHubUnreachableUtc!.Value;
        Assert.That(DeveloperHub.ReadState().FolderLimits!.Enforce, Is.True, "an outage doesn't change the last answer");
        Assert.That(DeveloperHub.FolderLimitEnforced(outage.AddDays(DeveloperHub.GraceDays)), Is.False, "a week without the hub");

        using (var off = new HttpClient(new RecordingHandler(HttpStatusCode.OK, """{"ok":true}""")))
        {
            await DeveloperHub.SendWeeklyAsync(new JsonObject(), off).ConfigureAwait(false);
        }

        Assert.That(DeveloperHub.FolderLimitEnforced(DateTime.UtcNow), Is.False, "missing limits: off");

        using (var update = new HttpClient(new RecordingHandler(HttpStatusCode.NotFound, """{"error":"no release","limits":{"enforce":true}}""")))
        {
            await DeveloperHub.CheckForUpdateAsync("HNMO-test", licenceFile: string.Empty, client: update).ConfigureAwait(false);
        }

        Assert.That(DeveloperHub.FolderLimitEnforced(DateTime.UtcNow), Is.True, "an update check's answer");

        using (var update = new HttpClient(new RecordingHandler(HttpStatusCode.OK, """{"manifestVersion":null}""")))
        {
            await DeveloperHub.CheckForUpdateAsync("HNMO-test", licenceFile: string.Empty, client: update).ConfigureAwait(false);
        }

        Assert.That(DeveloperHub.FolderLimitEnforced(DateTime.UtcNow), Is.False, "an update check's answer without limits");
    }

    /// <summary>(e) A folder which never reached the hub, or whose last answer is old, runs freely though another holds a seat.</summary>
    [Test]
    public void WithoutAFreshAnswerTheFolderRunsFreely()
    {
        var seatDir = Path.Combine(this._dir.FullName, "seats");
        this.Seats(seatDir, "aaaaaaaaaaaaaaaa", "/srv/A").Refresh(1);
        FolderSeat.Configure(this.Seats(seatDir, "bbbbbbbbbbbbbbbb", "/srv/B"), null);
        DeveloperHub.ChangeState(s => s.FolderLimits = new HubFolderLimits { Enforce = true, ReceivedUtc = this._now.AddDays(-15) });
        Assert.That(FolderSeat.Evaluate(this._now).IsBlocked, Is.False, "answer of 15 days ago");
        DeveloperHub.ChangeState(s =>
        {
            s.FolderLimits = new HubFolderLimits { Enforce = true, ReceivedUtc = this._now.AddDays(-1) };
            s.FirstHubUnreachableUtc = this._now.AddDays(-8);
        });
        Assert.That(FolderSeat.Evaluate(this._now).IsBlocked, Is.False, "hub unreachable for 8 days");
    }

    /// <summary>The launcher's update check keeps the switch in the folder's state file without touching the rest.</summary>
    [Test]
    public void TheLauncherKeepsTheSwitchInTheSharedStateFile()
    {
        var file = Path.Combine(this._dir.FullName, HubFolderLimits.StateFileName);
        DeveloperHub.StateFile = file;
        DeveloperHub.ChangeState(s => s.HostName = "Minh");
        Assert.That(HubFolderLimits.Remember(file, enforce: true, this._now), Is.True);
        var state = DeveloperHub.ReadState();
        Assert.That(state.HostName, Is.EqualTo("Minh"));
        Assert.That(state.FolderLimits, Is.Not.Null);
        Assert.That(state.FolderLimits!.Enforce, Is.True);
        Assert.That(state.FolderLimits.ReceivedUtc, Is.EqualTo(this._now));
        Assert.That(DeveloperHub.FolderLimitEnforced(this._now), Is.True);
    }

    /// <summary>
    /// (c) The folder which has been running longest holds the seat, though a younger one asked first (the hub switched
    /// the limit on while both ran).
    /// </summary>
    [Test]
    public void TheFolderRunningLongestKeepsTheSeat()
    {
        var seatDir = Path.Combine(this._dir.FullName, "seats");
        var olderStart = this._now.AddHours(-5);
        var older = this.Seats(seatDir, "bbbbbbbbbbbbbbbb", "/srv/B", olderStart);
        var younger = this.Seats(seatDir, "aaaaaaaaaaaaaaaa", "/srv/A", this._now.AddMinutes(-10));

        Assert.That(younger.Refresh(1).HasSeat, Is.True, "alone, the younger folder holds the seat");
        this._now = this._now.AddSeconds(1);
        var first = older.Refresh(1);
        Assert.That(first.HasSeat, Is.True, "the older folder, though it asked second (and its code sorts later)");
        Assert.That(first.OtherHolders, Is.Empty);
        var waiting = younger.Refresh(1);
        Assert.That(waiting.HasSeat, Is.False, "the younger folder waits");
        Assert.That(waiting.OtherHolders, Is.EqualTo(new[] { "/srv/B" }));
        Assert.That(SinceIn(older), Is.EqualTo(olderStart), "the file holds the process start");
    }

    /// <summary>(c) Two programs of one folder share one seat, and the seat file keeps the earliest start of them.</summary>
    [Test]
    public void ProgramsOfOneFolderShareASeatFromTheirEarliestStart()
    {
        var seatDir = Path.Combine(this._dir.FullName, "seats");
        var start = this._now;
        var seasonNine = this.Seats(seatDir, "aaaaaaaaaaaaaaaa", "/srv/A", start.AddMinutes(-5));
        var seasonSix = this.Seats(seatDir, "aaaaaaaaaaaaaaaa", "/srv/A", start.AddMinutes(-30));
        var other = this.Seats(seatDir, "bbbbbbbbbbbbbbbb", "/srv/B", start.AddMinutes(-20));

        Assert.That(seasonNine.Refresh(1).HasSeat, Is.True);
        Assert.That(SinceIn(seasonNine), Is.EqualTo(start.AddMinutes(-5)));
        Assert.That(other.Refresh(1).HasSeat, Is.True, "B started before A's first program");
        Assert.That(seasonNine.Refresh(1).HasSeat, Is.False, "so A waits");

        this._now = this._now.AddSeconds(5);
        Assert.That(seasonSix.Refresh(1).HasSeat, Is.True, "A's second program started before B");
        Assert.That(SinceIn(seasonSix), Is.EqualTo(start.AddMinutes(-30)), "the earliest start");
        this._now = this._now.AddSeconds(5);
        Assert.That(seasonNine.Refresh(1).HasSeat, Is.True, "both programs of A share the seat");
        Assert.That(SinceIn(seasonNine), Is.EqualTo(start.AddMinutes(-30)), "a later program doesn't move it forward");
        Assert.That(Directory.GetFiles(seatDir, "*.seat"), Has.Length.EqualTo(2), "one file per folder");
        Assert.That(other.Refresh(1).HasSeat, Is.False);
    }

    /// <summary>(c) Folders which started at the same time line up by folder code, whichever asked first.</summary>
    [Test]
    public void EqualStartsLineUpByFolderCode()
    {
        var seatDir = Path.Combine(this._dir.FullName, "seats");
        var started = this._now.AddMinutes(-1);
        var b = this.Seats(seatDir, "bbbbbbbbbbbbbbbb", "/srv/B", started);
        var a = this.Seats(seatDir, "aaaaaaaaaaaaaaaa", "/srv/A", started);
        Assert.That(b.Refresh(1).HasSeat, Is.True, "alone");
        Assert.That(a.Refresh(1).HasSeat, Is.True, "the lower code");
        Assert.That(b.Refresh(1).HasSeat, Is.False);
        Assert.That(a.Refresh(1).HasSeat, Is.True, "stable");
    }

    /// <summary>
    /// (c) An older folder which stops beating still frees its seat after the expiry; started again it is a young folder,
    /// and a program which only hung lines up from when it came back.
    /// </summary>
    [Test]
    public void ADeadOlderFolderStillReleasesItsSeat()
    {
        var seatDir = Path.Combine(this._dir.FullName, "seats");
        var older = this.Seats(seatDir, "aaaaaaaaaaaaaaaa", "/srv/A", this._now.AddHours(-3));
        var younger = this.Seats(seatDir, "bbbbbbbbbbbbbbbb", "/srv/B", this._now.AddHours(-1));
        Assert.That(older.Refresh(1).HasSeat, Is.True);
        Assert.That(younger.Refresh(1).HasSeat, Is.False);

        this._now = this._now.AddMinutes(1);
        Assert.That(younger.Refresh(1).HasSeat, Is.False, "A beat a minute ago");
        this._now = this._now.AddSeconds(90);
        Assert.That(younger.Refresh(1).HasSeat, Is.True, "A's seat expired");
        Assert.That(File.Exists(older.OwnFile), Is.False, "the dead seat file is removed");

        Assert.That(older.Refresh(1).HasSeat, Is.False, "the same program back after hanging lines up from now");
        Assert.That(SinceIn(older), Is.EqualTo(this._now));
        var restarted = this.Seats(seatDir, "aaaaaaaaaaaaaaaa", "/srv/A", this._now.AddSeconds(-10));
        Assert.That(restarted.Refresh(1).HasSeat, Is.False, "A started again is younger than B");
        Assert.That(younger.Refresh(1).HasSeat, Is.True);
    }

    /// <summary>
    /// The launcher's write keeps a change the server makes to the same state file meanwhile: the server's write, started
    /// between the launcher's read and write, waits for the lock and lands after it, on top of the launcher's.
    /// </summary>
    [Test]
    public void TheLauncherDoesNotLoseAServerWriteMadeWhileItWrites()
    {
        var file = Path.Combine(this._dir.FullName, HubFolderLimits.StateFileName);
        DeveloperHub.StateFile = file;
        DeveloperHub.ChangeState(s => s.HostName = "Minh");
        Task<DeveloperHubState>? server = null;
        var serverWaited = false;
        HubFolderLimits.AfterReadForTests = () =>
        {
            server = Task.Run(() => DeveloperHub.ChangeState(s => s.HostContact = "zalo 0123"));
            serverWaited = !server.Wait(TimeSpan.FromMilliseconds(300));
        };
        try
        {
            Assert.That(HubFolderLimits.Remember(file, enforce: true, this._now), Is.True);
        }
        finally
        {
            HubFolderLimits.AfterReadForTests = null;
        }

        Assert.That(serverWaited, Is.True, "the server's write waited for the launcher's lock");
        Assert.That(server!.Wait(TimeSpan.FromSeconds(10)), Is.True, "and then went through");
        var state = DeveloperHub.ReadState();
        Assert.That(state.HostName, Is.EqualTo("Minh"), "kept from before");
        Assert.That(state.HostContact, Is.EqualTo("zalo 0123"), "the server's write is kept");
        Assert.That(state.FolderLimits, Is.Not.Null, "the launcher's write is kept");
        Assert.That(state.FolderLimits!.Enforce, Is.True);
        Assert.That(state.FolderLimits.ReceivedUtc, Is.EqualTo(this._now));
    }

    /// <summary>
    /// The state file's lock is exclusive, is free again once given back, and a writer which can't get it in time writes
    /// all the same rather than not at all.
    /// </summary>
    [Test]
    public void TheStateFileLockIsExclusiveAndNeverStopsAWriter()
    {
        var file = Path.Combine(this._dir.FullName, HubFolderLimits.StateFileName);
        using (var held = SharedStateFile.Lock(file))
        {
            Assert.That(held, Is.Not.Null);
            Assert.That(SharedStateFile.Lock(file, TimeSpan.Zero), Is.Null, "a second writer doesn't get it");
            Assert.That(HubFolderLimits.Remember(file, enforce: true, this._now), Is.True, "after the wait, written without it");
        }

        using (var again = SharedStateFile.Lock(file, TimeSpan.Zero))
        {
            Assert.That(again, Is.Not.Null, "free once given back");
        }

        Assert.That(Directory.GetFiles(this._dir.FullName, "*.tmp"), Is.Empty, "no temporary file left");
    }

    private static (Ed25519PrivateKeyParameters Key, byte[] Pem) NewKey()
    {
        var generator = new Ed25519KeyPairGenerator();
        generator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
        var pair = generator.GenerateKeyPair();
        using var writer = new StringWriter();
        new PemWriter(writer).WriteObject(pair.Public);
        return ((Ed25519PrivateKeyParameters)pair.Private, Encoding.UTF8.GetBytes(writer.ToString()));
    }

    private static byte[] Signed(Ed25519PrivateKeyParameters key, string payloadJson)
    {
        var payload = Encoding.UTF8.GetBytes(payloadJson);
        var signer = new Ed25519Signer();
        signer.Init(true, key);
        signer.BlockUpdate(payload, 0, payload.Length);
        var signature = signer.GenerateSignature();
        return Encoding.UTF8.GetBytes($$"""{"payload":"{{Convert.ToBase64String(payload)}}","signature":"{{Convert.ToBase64String(signature)}}"}""");
    }

    private static DateTime SinceIn(FolderSeats seats)
        => JsonNode.Parse(File.ReadAllText(seats.OwnFile))!["since"]!.GetValue<DateTime>();

    private FolderSeats Seats(string directory, string folderCode, string path, DateTime? started = null)
        => new(directory, "machine1", folderCode, path, () => this._now, started ?? this._now);

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("simulated outage");
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
