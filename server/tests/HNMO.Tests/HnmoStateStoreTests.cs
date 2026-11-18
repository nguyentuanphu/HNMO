// <copyright file="HnmoStateStoreTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.AccountSafety;
using HNMO.GameLogic.PlugIns.Chat;
using HNMO.GameLogic.PlugIns.Community;
using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.GameLogic.PlugIns.Guilds;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.GameLogic.PlugIns.Onboarding;
using HNMO.GameLogic.PlugIns.ResetOptions;
using HNMO.GameLogic.PlugIns.TopRaces;
using HNMO.GameLogic.PlugIns.WorldBosses;
using NUnit.Framework;

/// <summary>
/// Tests for <see cref="HnmoStateStore"/>: unified storage, migration to <c>old/</c>,
/// concurrent atomic writes, and <c>.bak</c> recovery.
/// </summary>
[TestFixture]
public class HnmoStateStoreTests
{
    private string _saveDir = null!;

    /// <summary>Creates a fresh temporary save folder per test.</summary>
    [SetUp]
    public void SetUp()
    {
        this._saveDir = Path.Combine(Path.GetTempPath(), $"hnmo-state-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this._saveDir);
    }

    /// <summary>Cleans up the temporary save folder.</summary>
    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(this._saveDir))
        {
            Directory.Delete(this._saveDir, true);
        }
    }

    /// <summary>
    /// Verifies that all ten legacy JSON files in <c>save/</c> are migrated into <c>hnmo-state.json</c>
    /// and moved (not deleted) into <c>save/old/</c>.
    /// </summary>
    [Test]
    public void MigratesAllLegacyFilesIntoUnifiedStoreAndMovesToOldFolder()
    {
        var now = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
        var opts = new JsonSerializerOptions { WriteIndented = false };

        var safetyData = new AccountSafetyStoreData();
        safetyData.Accounts["alice"] = new SafetyAccountRecord { WrongTries = 2 };
        File.WriteAllText(Path.Combine(this._saveDir, "account-safety.json"), JsonSerializer.Serialize(safetyData, opts));

        var whisperData = new List<OfflineWhisper>
        {
            new("Bob", "Alice", "hello", now),
        };
        File.WriteAllText(Path.Combine(this._saveDir, "offline-whispers.json"), JsonSerializer.Serialize(whisperData, opts));

        var communityData = new CommunityStoreData();
        communityData.PaidDiscordUsers["discord-1"] = "alice";
        File.WriteAllText(Path.Combine(this._saveDir, "community.json"), JsonSerializer.Serialize(communityData, opts));

        var giftData = new GiftCodeStoreData();
        giftData.Uses["WELCOME"] = 3;
        File.WriteAllText(Path.Combine(this._saveDir, "gift-codes.json"), JsonSerializer.Serialize(giftData, opts));

        var guildData = new GuildStoreData();
        guildData.Today["g1"] = 42;
        File.WriteAllText(Path.Combine(this._saveDir, "guilds.json"), JsonSerializer.Serialize(guildData, opts));

        var missionData = new MissionStoreData();
        missionData.Accounts["alice"] = new MissionAccountState { InviteCode = "INV1" };
        File.WriteAllText(Path.Combine(this._saveDir, "missions.json"), JsonSerializer.Serialize(missionData, opts));

        var onboardingData = new OnboardingStoreData { ActivatedAtUtc = now };
        File.WriteAllText(Path.Combine(this._saveDir, "onboarding.json"), JsonSerializer.Serialize(onboardingData, opts));

        var resetData = new ResetStoreData();
        resetData.Characters["c1"] = new ResetCharacterRecord { Resets = 5 };
        File.WriteAllText(Path.Combine(this._saveDir, "resets.json"), JsonSerializer.Serialize(resetData, opts));

        var topRaceData = new TopRaceStoreData();
        topRaceData.Seen["c1"] = new TopRaceSeen { Level = 350 };
        File.WriteAllText(Path.Combine(this._saveDir, "top-races.json"), JsonSerializer.Serialize(topRaceData, opts));

        var bossData = new WorldBossStoreData();
        bossData.Bosses["kundun"] = new WorldBossState { NextSpawnUtc = now };
        File.WriteAllText(Path.Combine(this._saveDir, "world-bosses.json"), JsonSerializer.Serialize(bossData, opts));

        var unifiedPath = Path.Combine(this._saveDir, HnmoStateStore.StateFileName);
        Assert.That(File.Exists(unifiedPath), Is.False);

        var safety = new AccountSafetyStore(Path.Combine(this._saveDir, "account-safety.json"));
        var whispers = new OfflineWhisperStore(Path.Combine(this._saveDir, "offline-whispers.json"));
        var community = new CommunityStore(Path.Combine(this._saveDir, "community.json"));
        var gifts = new GiftCodeStore(Path.Combine(this._saveDir, "gift-codes.json"));
        var guilds = new GuildStore(Path.Combine(this._saveDir, "guilds.json"));
        var missions = new MissionStore(Path.Combine(this._saveDir, "missions.json"));
        var onboarding = new OnboardingStore(Path.Combine(this._saveDir, "onboarding.json"), now);
        var resets = new ResetStore(Path.Combine(this._saveDir, "resets.json"));
        var topRaces = new TopRaceStore(Path.Combine(this._saveDir, "top-races.json"));
        var bosses = new WorldBossStore(Path.Combine(this._saveDir, "world-bosses.json"));

        Assert.That(safety.Data.Accounts["alice"].WrongTries, Is.EqualTo(2));
        Assert.That(whispers.Count("Alice", now.AddMinutes(1)), Is.EqualTo(1));
        Assert.That(community.Data.PaidDiscordUsers["discord-1"], Is.EqualTo("alice"));
        Assert.That(gifts.Data.Uses["WELCOME"], Is.EqualTo(3));
        Assert.That(guilds.Data.Today["g1"], Is.EqualTo(42));
        Assert.That(missions.Data.Accounts["alice"].InviteCode, Is.EqualTo("INV1"));
        Assert.That(onboarding.Data.ActivatedAtUtc, Is.EqualTo(now));
        Assert.That(resets.Data.Characters["c1"].Resets, Is.EqualTo(5));
        Assert.That(topRaces.Data.Seen["c1"].Level, Is.EqualTo(350));
        Assert.That(bosses.Data.Bosses["kundun"].NextSpawnUtc, Is.EqualTo(now));

        Assert.That(File.Exists(unifiedPath), Is.True);
        Assert.That(File.Exists(unifiedPath + ".bak"), Is.True);

        var oldDir = Path.Combine(this._saveDir, HnmoStateStore.OldDirectoryName);
        string[] legacyNames =
        [
            "account-safety.json",
            "offline-whispers.json",
            "community.json",
            "gift-codes.json",
            "guilds.json",
            "missions.json",
            "onboarding.json",
            "resets.json",
            "top-races.json",
            "world-bosses.json",
        ];

        foreach (var name in legacyNames)
        {
            Assert.That(File.Exists(Path.Combine(this._saveDir, name)), Is.False, $"Legacy file {name} should be moved out of save/.");
            Assert.That(File.Exists(Path.Combine(oldDir, name)), Is.True, $"Legacy file {name} should be preserved in save/old/.");
        }
    }

    /// <summary>
    /// Verifies atomic writes under concurrent saves across multiple sections.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ConcurrentSavesAcrossSectionsStayAtomicAndValid()
    {
        var statePath = Path.Combine(this._saveDir, HnmoStateStore.StateFileName);
        var store = new HnmoStateStore(statePath);
        var opts = new JsonSerializerOptions { WriteIndented = false };

        var tasks = Enumerable.Range(0, 12).Select(worker => Task.Run(() =>
        {
            for (var i = 0; i < 20; i++)
            {
                store.WriteSection($"section-{worker}", new SampleSection { Counter = i, Worker = worker }, opts);
            }
        }));

        await Task.WhenAll(tasks).ConfigureAwait(false);

        Assert.That(File.Exists(statePath), Is.True);
        Assert.That(File.Exists(statePath + ".bak"), Is.True);

        var reloaded = new HnmoStateStore(statePath);
        for (var worker = 0; worker < 12; worker++)
        {
            var section = reloaded.ReadSection<SampleSection>($"section-{worker}", opts);
            Assert.That(section, Is.Not.Null);
            Assert.That(section!.Worker, Is.EqualTo(worker));
            Assert.That(section.Counter, Is.EqualTo(19));
        }
    }

    /// <summary>
    /// Verifies that a half-written or corrupted primary file recovers from <c>.bak</c>.
    /// </summary>
    [Test]
    public void HalfWrittenPrimaryRecoversFromBackup()
    {
        var statePath = Path.Combine(this._saveDir, HnmoStateStore.StateFileName);
        var opts = new JsonSerializerOptions { WriteIndented = false };

        var store = new HnmoStateStore(statePath);
        store.WriteSection("guilds", new SampleSection { Worker = 7, Counter = 99 }, opts);
        store.WriteSection("missions", new SampleSection { Worker = 8, Counter = 100 }, opts);

        // Simulate a crash mid-write that left truncated JSON in the primary file while .bak holds the previous state.
        File.WriteAllText(statePath, "{\"version\":1,\"sections\":{\"guilds\":{\"Worker\":7");

        var recovered = new HnmoStateStore(statePath);
        var guilds = recovered.ReadSection<SampleSection>("guilds", opts);
        Assert.That(guilds, Is.Not.Null);
        Assert.That(guilds!.Worker, Is.EqualTo(7));
        Assert.That(guilds.Counter, Is.EqualTo(99));
        Assert.That(File.Exists(statePath + ".broken"), Is.True);
    }

    private sealed class SampleSection
    {
        public int Worker { get; set; }

        public int Counter { get; set; }
    }
}
