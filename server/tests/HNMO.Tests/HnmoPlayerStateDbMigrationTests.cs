// <copyright file="HnmoPlayerStateDbMigrationTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.AccountSafety;
using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.GameLogic.PlugIns.TopRaces;
using HNMO.Persistence;
using HNMO.Persistence.EntityFramework;

/// <summary>
/// Tests per-account / per-character row storage, automatic migration from 5,000 legacy accounts in JSON,
/// individual O(1) row updates, and finished/expired archival.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class HnmoPlayerStateDbMigrationTests
{
    private string _tempDir = null!;
    private string _stateFile = null!;

    /// <summary>Sets up an isolated temp folder and row backend.</summary>
    [SetUp]
    public void SetUp()
    {
        HnmoPlayerStateRowStoreRegistry.ResetForTests();
        this._tempDir = Path.Combine(Path.GetTempPath(), $"hnmo-p1a-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this._tempDir);
        this._stateFile = Path.Combine(this._tempDir, "hnmo-state.json");
    }

    /// <summary>Cleans up temp files and registry overrides.</summary>
    [TearDown]
    public void TearDown()
    {
        HnmoPlayerStateRowStoreRegistry.ResetForTests();
        if (Directory.Exists(this._tempDir))
        {
            Directory.Delete(this._tempDir, recursive: true);
        }
    }

    /// <summary>
    /// Verifies first-start migration of 5,000 legacy JSON accounts + characters into individual rows,
    /// shrinking <c>hnmo-state.json</c>, O(1) single-row updates on subsequent saves, and reload integrity.
    /// </summary>
    [Test]
    public void MigratesFiveThousandLegacyAccountsToRowsAndUpdatesIndividually()
    {
        const int accountCount = 5000;
        var legacyMissions = new MissionStoreData();
        var legacySafety = new AccountSafetyStoreData();
        for (var i = 0; i < accountCount; i++)
        {
            var login = $"acc{i:D4}";
            var charId = $"char{i:D4}";
            legacyMissions.Accounts[login] = new MissionAccountState { DayKey = "2026-11-16", EarnedToday = i % 50, InviteCode = $"INV{i:D4}" };
            legacyMissions.Characters[charId] = new MissionCharacterState { Account = login, Name = $"Hero{i}", StreakCount = (i % 7) + 1 };
            legacySafety.Accounts[login] = new SafetyAccountRecord { WrongTries = i % 3 };
        }

        var unified = HnmoStateStore.Open(this._stateFile)!;
        unified.WriteSection("missions", legacyMissions, new JsonSerializerOptions());
        unified.WriteSection("accountSafety", legacySafety, new JsonSerializerOptions());
        var legacySizeBytes = new FileInfo(this._stateFile).Length;

        var spy = new SpyRowBackend(new InMemoryHnmoPlayerStateRowBackend());
        HnmoPlayerStateRowStoreRegistry.RegisterBackend(this._stateFile, spy);

        // First load triggers automatic migration into row backend and clears legacy JSON sections.
        var missionStore = new MissionStore(this._stateFile);
        var safetyStore = new AccountSafetyStore(this._stateFile);

        Assert.That(missionStore.Data.Accounts.Count, Is.EqualTo(accountCount));
        Assert.That(missionStore.Data.Characters.Count, Is.EqualTo(accountCount));
        Assert.That(safetyStore.Data.Accounts.Count, Is.EqualTo(accountCount));
        Assert.That(spy.Inner.CountRows("missions.account"), Is.EqualTo(accountCount));
        Assert.That(spy.Inner.CountRows("missions.char"), Is.EqualTo(accountCount));
        Assert.That(spy.Inner.CountRows("accountSafety.account"), Is.EqualTo(accountCount));

        var shrunkSizeBytes = new FileInfo(this._stateFile).Length;
        Assert.That(shrunkSizeBytes, Is.LessThan(legacySizeBytes / 10));

        // Update ONE account and verify only 1 row is written (not 5,000).
        spy.UpsertCalls.Clear();
        missionStore.Account("acc2500").EarnedToday += 25;
        missionStore.MarkAccountDirty("acc2500");
        missionStore.SaveIfChanged();

        Assert.That(spy.UpsertCalls.Sum(c => c.Upserts), Is.EqualTo(1));

        // Fresh store instances reload from the row backend with full data preserved.
        var reloadedMissions = new MissionStore(this._stateFile);
        var reloadedSafety = new AccountSafetyStore(this._stateFile);
        Assert.That(reloadedMissions.Account("acc2500").EarnedToday, Is.EqualTo((2500 % 50) + 25));
        Assert.That(reloadedMissions.Character("char4999").StreakCount, Is.EqualTo((4999 % 7) + 1));
        Assert.That(reloadedSafety.Data.Accounts["acc0005"].WrongTries, Is.EqualTo(2));
    }

    /// <summary>
    /// Verifies <see cref="HnmoStateRetentionPlugIn.ArchiveNow"/> archives expired gift code claims and old ended top-race
    /// runs while keeping active or recent rows.
    /// </summary>
    [Test]
    public void ArchivesExpiredGiftClaimsAndEndedTopRaceRunsAfterConfiguredDays()
    {
        var now = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
        var backend = new InMemoryHnmoPlayerStateRowBackend();
        HnmoPlayerStateRowStoreRegistry.RegisterBackend(this._stateFile, backend);

        var giftStore = new GiftCodeStore(this._stateFile);
        lock (giftStore.Lock)
        {
            giftStore.Data.Codes.Add(new GiftCodeDefinition { Id = "old-code", Code = "OLD1", End = "2026-10-01 00:00" });
            giftStore.Data.Codes.Add(new GiftCodeDefinition { Id = "recent-code", Code = "NEW1", End = "2026-11-10 00:00" });
            giftStore.Data.Claims["old-code"] = ["a:acc1"];
            giftStore.Data.Claims["recent-code"] = ["a:acc2"];
            giftStore.Changed();
            giftStore.SaveIfChanged();
        }

        var raceStore = new TopRaceStore(this._stateFile);
        lock (raceStore.Lock)
        {
            raceStore.Data.Runs["old-run"] = new TopRaceRun
            {
                RaceId = "r1",
                PeriodKey = "w1",
                StartUtc = now.AddDays(-50),
                EndUtc = now.AddDays(-40),
            };
            raceStore.Data.Runs["active-run"] = new TopRaceRun
            {
                RaceId = "r2",
                PeriodKey = "w2",
                StartUtc = now.AddDays(-1),
                EndUtc = now.AddDays(6),
            };
            raceStore.Changed();
            raceStore.SaveIfChanged();
        }

        var plugin = new HnmoStateRetentionPlugIn
        {
            Configuration = new HnmoStateRetentionConfiguration
            {
                FileName = this._stateFile,
                ArchiveFinishedDataAfterDays = 30,
            },
        };

        var archived = plugin.ArchiveNow(now);

        Assert.That(archived, Is.EqualTo(2));
        Assert.That(backend.CountArchivedRows(), Is.EqualTo(2));
        Assert.That(backend.LoadAll("giftCodes.claims").Select(r => r.ScopeId), Is.EqualTo(new[] { "recent-code" }));
        Assert.That(backend.LoadAll("topRaces.run").Select(r => r.ScopeId), Is.EqualTo(new[] { "active-run" }));
    }

    /// <summary>Validates <see cref="PostgresHnmoPlayerStateRowBackend"/> constructor guard.</summary>
    [Test]
    public void PostgresRowBackendValidatesConnectionString()
    {
        Assert.Throws<ArgumentException>(() => _ = new PostgresHnmoPlayerStateRowBackend(" "));
    }

    private sealed class SpyRowBackend(InMemoryHnmoPlayerStateRowBackend inner) : IHnmoPlayerStateRowBackend
    {
        public InMemoryHnmoPlayerStateRowBackend Inner { get; } = inner;

        public List<(int Upserts, int Deletes)> UpsertCalls { get; } = [];

        public void EnsureSchema() => this.Inner.EnsureSchema();

        public IReadOnlyList<HnmoFeatureRow> LoadAll(string feature) => this.Inner.LoadAll(feature);

        public void UpsertBatch(IReadOnlyList<HnmoFeatureRow> upserts, IReadOnlyList<(string Feature, string ScopeId)> deletes)
        {
            this.UpsertCalls.Add((upserts.Count, deletes.Count));
            this.Inner.UpsertBatch(upserts, deletes);
        }

        public int ArchiveExpired(DateTime cutoffUtc, string? feature = null) => this.Inner.ArchiveExpired(cutoffUtc, feature);

        public int CountRows(string? feature = null) => this.Inner.CountRows(feature);

        public int CountArchivedRows(string? feature = null) => this.Inner.CountArchivedRows(feature);
    }
}
