// <copyright file="SeasonPlanEngineTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using HNMO.GameLogic.PlugIns.SeasonPlan;

/// <summary>
/// Unit tests for <see cref="SeasonPlanEngine"/>: date triggers, boss kill triggers, player level triggers,
/// manual admin toggle, and feature gate checks.
/// </summary>
[TestFixture]
public class SeasonPlanEngineTest
{
    private string _tempDir = string.Empty;

    /// <summary>Creates an isolated temp folder.</summary>
    [SetUp]
    public void SetUp()
    {
        this._tempDir = Path.Combine(Path.GetTempPath(), $"hnmo-sp-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this._tempDir);
    }

    /// <summary>Deletes temp folder.</summary>
    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(this._tempDir))
        {
            Directory.Delete(this._tempDir, recursive: true);
        }
    }

    /// <summary>Dates, boss kills, and N players at a level each unlock their stages cleanly and persist in HnmoStateStore.</summary>
    [Test]
    public void StagesUnlockByDateBossAndPlayerCount()
    {
        var day0 = new DateTime(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc);
        var cfg = new SeasonPlanConfiguration
        {
            FileName = Path.Combine(this._tempDir, "hnmo-state.json"),
            Stages =
            [
                new() { Id = "s1", NameEn = "Launch", UnlockAtUtc = day0, MaxBoxLevel = 2 },
                new() { Id = "s2", NameEn = "Master & +15", Unlocks = [SeasonFeatureGate.ItemPlus10To15, SeasonFeatureGate.MasterLevels], RequiredPlayerLevel = 400, RequiredPlayersCount = 2, MaxBoxLevel = 3 },
                new() { Id = "s3", NameEn = "Wing 2", Unlocks = [SeasonFeatureGate.Wing2], RequiredBossId = "red-dragon", RequiredBossKills = 1, MaxBoxLevel = 4 },
                new() { Id = "s4", NameEn = "Wing 3", Unlocks = [SeasonFeatureGate.Wing3], UnlockAtUtc = day0.AddDays(14), MaxBoxLevel = 5 },
            ],
        };

        var engine = new SeasonPlanEngine(cfg, SeasonPlanStore.Open(cfg.FileName), enabled: true);

        // At day0: s1 unlocked by date; +10..+15, MasterLevels, Wing2, Wing3 are locked; MaxBoxLevel is 2
        Assert.That(engine.IsFeatureUnlocked(SeasonFeatureGate.Harmony, day0), Is.True, "Unreferenced gates remain allowed.");
        Assert.That(engine.IsFeatureUnlocked(SeasonFeatureGate.ItemPlus10To15, day0), Is.False);
        Assert.That(engine.IsFeatureUnlocked(SeasonFeatureGate.MasterLevels, day0), Is.False);
        Assert.That(engine.IsFeatureUnlocked(SeasonFeatureGate.Wing2, day0), Is.False);
        Assert.That(engine.GetCurrentMaxBoxLevel(day0), Is.EqualTo(2));

        // 1 player at Lv.400 is not enough (needs 2)
        engine.ObserveCharacterLevel("char1", 400, day0);
        Assert.That(engine.IsFeatureUnlocked(SeasonFeatureGate.MasterLevels, day0), Is.False);

        // 2nd distinct player at Lv.400 unlocks s2!
        var unlocked2 = engine.ObserveCharacterLevel("char2", 400, day0);
        Assert.That(unlocked2.Select(s => s.Id), Is.EqualTo(new[] { "s2" }));
        Assert.That(engine.IsFeatureUnlocked(SeasonFeatureGate.ItemPlus10To15, day0), Is.True);
        Assert.That(engine.IsFeatureUnlocked(SeasonFeatureGate.MasterLevels, day0), Is.True);
        Assert.That(engine.GetCurrentMaxBoxLevel(day0), Is.EqualTo(3));

        // Killing red-dragon unlocks s3 (Wing2)
        var unlocked3 = engine.ObserveBossKill("red-dragon", day0);
        Assert.That(unlocked3.Select(s => s.Id), Is.EqualTo(new[] { "s3" }));
        Assert.That(engine.IsFeatureUnlocked(SeasonFeatureGate.Wing2, day0), Is.True);
        Assert.That(engine.GetCurrentMaxBoxLevel(day0), Is.EqualTo(4));

        // Scheduled date day0+14 unlocks s4 (Wing3)
        Assert.That(engine.IsFeatureUnlocked(SeasonFeatureGate.Wing3, day0.AddDays(13)), Is.False);
        Assert.That(engine.IsFeatureUnlocked(SeasonFeatureGate.Wing3, day0.AddDays(14)), Is.True);
        Assert.That(engine.GetCurrentMaxBoxLevel(day0.AddDays(14)), Is.EqualTo(5));

        // Admin manual relock works
        engine.SetStageUnlocked("s4", false, day0.AddDays(1));
        Assert.That(engine.IsFeatureUnlocked(SeasonFeatureGate.Wing3, day0.AddDays(1)), Is.False);
    }
}
