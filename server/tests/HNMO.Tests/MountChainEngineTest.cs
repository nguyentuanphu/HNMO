// <copyright file="MountChainEngineTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using HNMO.GameLogic.PlugIns.MountChain;

/// <summary>
/// Unit tests for <see cref="MountChainEngine"/>.
/// </summary>
[TestFixture]
public class MountChainEngineTest
{
    private string _tempDir = string.Empty;

    /// <summary>Creates temp directory.</summary>
    [SetUp]
    public void SetUp()
    {
        this._tempDir = Path.Combine(Path.GetTempPath(), $"hnmo-mc-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this._tempDir);
    }

    /// <summary>Deletes temp directory.</summary>
    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(this._tempDir))
        {
            Directory.Delete(this._tempDir, recursive: true);
        }
    }

    /// <summary>Verifies Golden Boss detection, craft upgrade with duplicate mounts, salvage, and active tier switching.</summary>
    [Test]
    public void CraftSalvageAndGoldenDetectionWorkCleanly()
    {
        var cfg = new MountChainConfiguration();
        var store = MountChainStore.Open(Path.Combine(this._tempDir, "state.json"));
        var engine = new MountChainEngine(cfg, store, enabled: true);

        Assert.That(engine.IsGoldenBoss(79, "Monster"), Is.True);
        Assert.That(engine.IsGoldenBoss(999, "Golden Knight"), Is.True);
        Assert.That(engine.IsGoldenBoss(1, "Budge Dragon"), Is.False);

        Assert.That(engine.AwardShards("alice", 10), Is.EqualTo(10));

        // Craft Uniria (3 shards, no previous mount).
        Assert.That(engine.PlanCraft("alice", "horn-uniria", [], 1_000_000, out _), Is.EqualTo(MountActionResult.Ok));
        engine.CompleteCraft("alice", cfg.Tiers[0], DateTime.UtcNow);
        Assert.That(engine.GetActiveTier("alice")?.Id, Is.EqualTo("horn-uniria"));

        // Dinorant requires 6 shards + 1 unlocked Uniria in bag (slot 12 locked is skipped!).
        var lockedOnly = new List<MountBagItem> { new(12, 13, 2, 0, Locked: true) };
        Assert.That(engine.PlanCraft("alice", "dinorant", lockedOnly, 2_000_000, out _), Is.EqualTo(MountActionResult.MissingPreviousMount));

        var withDuplicate = new List<MountBagItem> { new(13, 13, 2, 0, Locked: false), new(14, 13, 2, 0, Locked: false) };
        Assert.That(engine.PlanCraft("alice", "dinorant", withDuplicate, 2_000_000, out var picked), Is.EqualTo(MountActionResult.Ok));
        Assert.That(picked, Is.EqualTo(new byte[] { 13 }));
        engine.CompleteCraft("alice", cfg.Tiers[1], DateTime.UtcNow);
        Assert.That(engine.GetActiveTier("alice")?.Id, Is.EqualTo("dinorant"));

        // Switch back to Uniria and salvage spare Uniria in slot 14.
        Assert.That(engine.ActivateTier("alice", "horn-uniria"), Is.EqualTo(MountActionResult.Ok));
        Assert.That(engine.GetActiveTier("alice")?.Id, Is.EqualTo("horn-uniria"));
        Assert.That(engine.PlanSalvage("horn-uniria", [new(14, 13, 2, 0, false)], out var salvageSlot), Is.EqualTo(MountActionResult.Ok));
        Assert.That(salvageSlot, Is.EqualTo((byte)14));
    }
}
