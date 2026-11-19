// <copyright file="CombiningLeftoversEngineTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using HNMO.GameLogic.PlugIns.CombiningLeftovers;
using HNMO.GameLogic.PlugIns.GiftCodes;

/// <summary>
/// Unit tests for <see cref="CombiningLeftoversEngine"/> and <see cref="CombiningLeftoversStore"/>.
/// </summary>
[TestFixture]
public class CombiningLeftoversEngineTest
{
    private string _tempDir = string.Empty;

    /// <summary>Creates temp directory.</summary>
    [SetUp]
    public void SetUp()
    {
        this._tempDir = Path.Combine(Path.GetTempPath(), $"hnmo-lo-{Guid.NewGuid():N}");
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

    /// <summary>Combine selects exact unlocked bag slots, checks Zen and season box cap, and respects locked items.</summary>
    [Test]
    public void PlanCombineRespectsSeasonCapZenLocksAndIngredients()
    {
        var cfg = new CombiningLeftoversConfiguration { MaxBoxLevel = 5 };
        var store = CombiningLeftoversStore.Open(Path.Combine(this._tempDir, "state.json"));
        var engine = new CombiningLeftoversEngine(cfg, store, enabled: true);

        var bag = new List<BagItemDescriptor>
        {
            new(12, 14, 11, 9, Locked: true),  // locked Box+2 ignored
            new(13, 14, 11, 9, Locked: false), // unlocked Box+2
            new(14, 14, 11, 9, Locked: false), // unlocked Box+2
            new(15, 14, 16, 0, Locked: false), // JoL
            new(16, 14, 22, 0, Locked: false), // JoC
        };

        Assert.That(engine.PlanCombine("box-2-to-3", bag, 2_000_000, seasonCapOverride: 2, out _), Is.EqualTo(LeftoversCheckResult.BlockedBySeasonStage));
        Assert.That(engine.PlanCombine("box-2-to-3", bag, 500_000, seasonCapOverride: 3, out _), Is.EqualTo(LeftoversCheckResult.NotEnoughZen));

        var ok = engine.PlanCombine("box-2-to-3", bag, 2_000_000, seasonCapOverride: 3, out var picked);
        Assert.That(ok, Is.EqualTo(LeftoversCheckResult.Ok));
        Assert.That(picked, Is.EquivalentTo(new byte[] { 13, 14, 15, 16 }));

        // Removing one unlocked Box+2 fails even though locked slot 12 has Box+2.
        bag.RemoveAt(1);
        Assert.That(engine.PlanCombine("box-2-to-3", bag, 2_000_000, seasonCapOverride: 3, out _), Is.EqualTo(LeftoversCheckResult.MissingIngredients));
    }

    /// <summary>Exchange selects surplus items and owed reward queue round-trips cleanly.</summary>
    [Test]
    public void PlanExchangeAndOwedRoundTrip()
    {
        var cfg = new CombiningLeftoversConfiguration();
        var store = CombiningLeftoversStore.Open(Path.Combine(this._tempDir, "state.json"));
        var engine = new CombiningLeftoversEngine(cfg, store, enabled: true);

        var bag = new List<BagItemDescriptor>
        {
            new(12, 14, 21, 0),
            new(13, 14, 21, 0),
            new(14, 14, 21, 0),
            new(15, 14, 21, 0),
            new(16, 14, 21, 0),
        };

        Assert.That(engine.PlanExchange("ex-rena", bag, out var slots), Is.EqualTo(LeftoversCheckResult.Ok));
        Assert.That(slots, Has.Count.EqualTo(5));

        store.AddOwed("char-1", new GiftCodeOwedReward { Reference = "r1", Zen = 100 });
        var taken = store.TakeOwed("char-1");
        Assert.That(taken, Has.Count.EqualTo(1));
        Assert.That(store.TakeOwed("char-1"), Is.Empty);
    }
}
