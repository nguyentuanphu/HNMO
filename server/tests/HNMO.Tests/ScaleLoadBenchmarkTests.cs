// <copyright file="ScaleLoadBenchmarkTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.AccountSafety;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.GameLogic.PlugIns.Onboarding;
using HNMO.GameLogic.PlugIns.ResetOptions;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;

/// <summary>
/// Part 1C (ROADMAP 15.1-15.3): Simulated 1,000-account + 2,000-character scale test measuring
/// save latency, login lookup time, storage footprint, and managed/process memory.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class ScaleLoadBenchmarkTests
{
    private string _tempDir = null!;

    /// <summary>Sets up an isolated temp folder and resets row store registry.</summary>
    [SetUp]
    public void SetUp()
    {
        HnmoPlayerStateRowStoreRegistry.ResetForTests();
        this._tempDir = Path.Combine(Path.GetTempPath(), $"hnmo-load-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this._tempDir);
    }

    /// <summary>Cleans up temporary test data.</summary>
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
    /// Simulates 1,000 accounts with 2 characters each and measures save time, login load time,
    /// row/JSON database footprint, and memory usage.
    /// </summary>
    /// <returns>The async task.</returns>
    [Test]
    public async Task SimulatesThousandAccountsAndReportsLatencyDbSizeAndMemory()
    {
        const int accountCount = 1000;
        const int charsPerAccount = 2;
        const int sampleUpdates = 100;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var managedBefore = GC.GetTotalMemory(true);
        var workingSetBefore = Process.GetCurrentProcess().WorkingSet64;

        var provider = new InMemoryPersistenceContextProvider();
        GameConfiguration config;
        using (var initCtx = provider.CreateNewContext())
        {
            config = initCtx.CreateNew<GameConfiguration>();
            var cls = initCtx.CreateNew<CharacterClass>();
            cls.Name = "Dark Knight";
            cls.Number = 16;
            config.CharacterClasses.Add(cls);
            await initCtx.SaveChangesAsync().ConfigureAwait(false);
        }

        // Seed 1,000 accounts x 2 characters.
        var seedSw = Stopwatch.StartNew();
        using (var playerCtx = provider.CreateNewPlayerContext(config))
        {
            var dkClass = config.CharacterClasses.First();
            for (var i = 0; i < accountCount; i++)
            {
                var acc = playerCtx.CreateNew<Account>();
                acc.LoginName = $"loadacc{i:D4}";
                acc.PasswordHash = "hash";
                acc.VaultPassword = "pin";
                for (var c = 0; c < charsPerAccount; c++)
                {
                    var ch = playerCtx.CreateNew<Character>();
                    ch.Name = $"LoadHero{i:D4}_{c}";
                    ch.CharacterClass = dkClass;
                    ch.CharacterSlot = (byte)c;
                    acc.Characters.Add(ch);
                }
            }

            await playerCtx.SaveChangesAsync().ConfigureAwait(false);
        }

        seedSw.Stop();

        // Populate per-account/per-character plug-in feature rows for all 1,000 accounts + 2,000 characters.
        var stateFile = Path.Combine(this._tempDir, "hnmo-state.json");
        var backend = new MeasuringRowBackend();
        HnmoPlayerStateRowStoreRegistry.RegisterBackend(stateFile, backend);

        var missions = new MissionStore(stateFile);
        var safety = new AccountSafetyStore(stateFile);
        var resets = new ResetStore(stateFile);
        var onboarding = new OnboardingStore(stateFile);

        var bulkPopulateSw = Stopwatch.StartNew();
        for (var i = 0; i < accountCount; i++)
        {
            var login = $"loadacc{i:D4}";
            missions.Account(login).EarnedToday = i % 100;
            safety.Data.Accounts[login] = new SafetyAccountRecord { WrongTries = i % 3 };
            onboarding.Data.Accounts[login] = new OnboardingAccountState();
            for (var c = 0; c < charsPerAccount; c++)
            {
                var cid = $"char_{i:D4}_{c}";
                missions.Character(cid).StreakCount = (i % 7) + 1;
                resets.Data.Characters[cid] = new ResetCharacterRecord { Resets = i % 20 };
            }
        }

        missions.Changed();
        safety.Changed();
        onboarding.Changed();
        resets.Changed();
        missions.SaveIfChanged();
        safety.SaveIfChanged();
        onboarding.SaveIfChanged();
        resets.SaveIfChanged();
        bulkPopulateSw.Stop();

        // Measure single-account O(1) incremental save latency across 100 random account mutations.
        var singleSaveTimesMs = new double[sampleUpdates];
        for (var s = 0; s < sampleUpdates; s++)
        {
            var login = $"loadacc{(s * 9) % accountCount:D4}";
            var sw = Stopwatch.StartNew();
            missions.Account(login).EarnedToday += 5;
            missions.MarkAccountDirty(login);
            missions.SaveIfChanged();
            sw.Stop();
            singleSaveTimesMs[s] = sw.Elapsed.TotalMilliseconds;
        }

        // Measure 1,000 simulated logins (account authentication lookup + feature row lookup + character selection).
        var loginTimesMs = new double[accountCount];
        using (var readCtx = provider.CreateNewPlayerContext(config))
        {
            for (var i = 0; i < accountCount; i++)
            {
                var login = $"loadacc{i:D4}";
                var sw = Stopwatch.StartNew();
                var acc = await readCtx.GetAccountByLoginNameAsync(login, default).ConfigureAwait(false);
                var missionAcc = missions.Account(login);
                var safetyAcc = safety.Data.Accounts[login];
                sw.Stop();
                Assert.That(acc, Is.Not.Null);
                Assert.That(acc!.Characters.Count, Is.EqualTo(charsPerAccount));
                Assert.That(missionAcc, Is.Not.Null);
                Assert.That(safetyAcc, Is.Not.Null);
                loginTimesMs[i] = sw.Elapsed.TotalMilliseconds;
            }
        }

        // Compare legacy monolithic JSON rewrite size vs individual row storage.
        var legacyJsonFile = Path.Combine(this._tempDir, "legacy-state.json");
        var legacySw = Stopwatch.StartNew();
        var legacyJsonText = System.Text.Json.JsonSerializer.Serialize(new
        {
            missions = missions.Data,
            safety = safety.Data,
            resets = resets.Data,
            onboarding = onboarding.Data,
        });
        File.WriteAllText(legacyJsonFile, legacyJsonText);
        legacySw.Stop();
        var legacyJsonBytes = new FileInfo(legacyJsonFile).Length;

        var managedAfter = GC.GetTotalMemory(true);
        var workingSetAfter = Process.GetCurrentProcess().WorkingSet64;

        Array.Sort(singleSaveTimesMs);
        Array.Sort(loginTimesMs);
        var avgSaveMs = singleSaveTimesMs.Average();
        var p95SaveMs = singleSaveTimesMs[(int)(sampleUpdates * 0.95)];
        var avgLoginMs = loginTimesMs.Average();
        var p95LoginMs = loginTimesMs[(int)(accountCount * 0.95)];
        var jsonSizeBytes = File.Exists(stateFile) ? new FileInfo(stateFile).Length : 0;
        var dbRowPayloadBytes = backend.TotalPayloadBytes();
        var totalRows = backend.CountRows();
        var managedDeltaMb = Math.Max(0, (managedAfter - managedBefore) / (1024.0 * 1024.0));
        var workingSetMb = workingSetAfter / (1024.0 * 1024.0);

        TestContext.Out.WriteLine(
            $"LOAD REPORT (1,000 accounts, 2,000 characters): " +
            $"Seed={seedSw.ElapsedMilliseconds}ms, BulkFeatureRows={bulkPopulateSw.ElapsedMilliseconds}ms, " +
            $"SingleSave avg={avgSaveMs:F3}ms p95={p95SaveMs:F3}ms vs LegacyWholeJsonSave={legacySw.Elapsed.TotalMilliseconds:F3}ms ({legacyJsonBytes} bytes), " +
            $"Login avg={avgLoginMs:F3}ms p95={p95LoginMs:F3}ms, " +
            $"Rows={totalRows}, DbPayloadBytes={dbRowPayloadBytes}, StateJsonBytes={jsonSizeBytes}, " +
            $"ManagedDelta={managedDeltaMb:F2}MB, ProcessWorkingSet={workingSetMb:F2}MB (before={workingSetBefore / (1024.0 * 1024.0):F2}MB)");

        Assert.That(totalRows, Is.EqualTo((accountCount * 3) + (accountCount * charsPerAccount * 2)));
        Assert.That(legacyJsonBytes, Is.GreaterThan(200_000), "Legacy unified dump is >200KB for 1,000 accounts");
        Assert.That(jsonSizeBytes, Is.LessThan(4096), "Unified JSON must remain tiny when per-player state is in DB rows");
        Assert.That(avgSaveMs, Is.LessThan(20.0), "Incremental single-account save should stay well under 20 ms");
        Assert.That(avgLoginMs, Is.LessThan(20.0), "Simulated login should stay well under 20 ms");
    }

    private sealed class MeasuringRowBackend : IHnmoPlayerStateRowBackend
    {
        private readonly InMemoryHnmoPlayerStateRowBackend _inner = new();
        private readonly Dictionary<(string, string), int> _payloadBytes = new();

        public void EnsureSchema() => this._inner.EnsureSchema();

        public IReadOnlyList<HnmoFeatureRow> LoadAll(string feature) => this._inner.LoadAll(feature);

        public void UpsertBatch(IReadOnlyList<HnmoFeatureRow> upserts, IReadOnlyList<(string Feature, string ScopeId)> deletes)
        {
            foreach (var u in upserts)
            {
                this._payloadBytes[(u.Feature, u.ScopeId)] = System.Text.Encoding.UTF8.GetByteCount(u.PayloadJson);
            }

            foreach (var d in deletes)
            {
                this._payloadBytes.Remove(d);
            }

            this._inner.UpsertBatch(upserts, deletes);
        }

        public int ArchiveExpired(DateTime cutoffUtc, string? feature = null) => this._inner.ArchiveExpired(cutoffUtc, feature);

        public int CountRows(string? feature = null) => this._inner.CountRows(feature);

        public int CountArchivedRows(string? feature = null) => this._inner.CountArchivedRows(feature);

        public long TotalPayloadBytes() => this._payloadBytes.Values.Sum(v => (long)v);
    }
}
