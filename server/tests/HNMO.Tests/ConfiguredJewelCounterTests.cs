// <copyright file="ConfiguredJewelCounterTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using AccountState = HNMO.DataModel.Entities.AccountState;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.GameLogic.PlugIns.Economy;
using HNMO.Persistence.BasicModel;

/// <summary>Verifies real bundle sizes and eligibility for the website's economy projection.</summary>
[TestFixture]
public sealed class ConfiguredJewelCounterTests
{
    /// <summary>A single in the bundle group stays a single, and a configured bundle can use a custom group.</summary>
    [Test]
    public void CountsConfiguredBundlesWithoutInferringFromItemGroups()
    {
        var single = new ItemDefinition { Group = 12, Number = 15 };
        var bundle = new ItemDefinition { Group = 9, Number = 50 };
        var mix = new JewelMix { SingleJewel = single, MixedJewel = bundle };
        var counter = new ConfiguredJewelCounter([mix], [(14, 13)]);
        Assert.That(counter.Count(12, 15, 0, 1), Is.EqualTo(1));
        Assert.That(counter.Count(9, 50, 1, 1), Is.EqualTo(20));
        Assert.That(counter.Count(9, 50, 5, 1), Is.Zero);
        Assert.That(counter.Count(14, 13, 0, 4), Is.EqualTo(4));
        Assert.That(counter.Count(12, 16, 0, 1), Is.Zero);
    }

    /// <summary>A capture excludes banned holdings and counts only retained monster loot once.</summary>
    [Test]
    public void CaptureCountsOnlyEligibleHoldingsAndRecordedMonsterLoot()
    {
        var single = new ItemDefinition { Group = 12, Number = 15 };
        var bundle = new ItemDefinition { Group = 9, Number = 50 };
        var counter = new ConfiguredJewelCounter([new JewelMix { SingleJewel = single, MixedJewel = bundle }], []);
        var active = new Account { LoginName = "first-fixture", Vault = new ItemStorage() };
        var banned = new Account { LoginName = "banned-fixture", State = AccountState.Banned, Vault = new ItemStorage() };
        active.Vault.Items.Add(new Item { Definition = single });
        banned.Vault.Items.Add(new Item { Definition = bundle, Level = 2 });
        var origin = new ActivityItemState(Guid.NewGuid(), 9, 50, 1, 1, 1, 0, false, string.Empty, string.Empty);
        var other = origin with { Serial = Guid.NewGuid() };
        var now = DateTime.UnixEpoch;
        var tracked = EconomyJewelActivity.Capture([active, banned],
        [
            new() { Kind = ActivityKind.Loot, TimestampUtc = now, AfterItems = [origin, other] },
            new() { Kind = ActivityKind.Pickup, TimestampUtc = now.AddSeconds(1), Account = active.LoginName, Character = "FixtureA", PickupQuantity = 1, BeforeItems = [origin] },
            new() { Kind = ActivityKind.Pickup, TimestampUtc = now.AddSeconds(2), Account = active.LoginName, Character = "FixtureA", PickupQuantity = 1, BeforeItems = [origin] },
            new() { Kind = ActivityKind.Pickup, TimestampUtc = now.AddSeconds(3), Account = banned.LoginName, Character = "FixtureB", PickupQuantity = 1, BeforeItems = [other] },
        ], false, counter);
        Assert.That(tracked.InventoryAndVaultPieces, Is.EqualTo(1));
        Assert.That(tracked.Hunters.Select(row => (row.Character, row.Pieces)), Is.EqualTo(new[] { ("FixtureA", 20L) }));
    }

    /// <summary>Either persisted ban mode excludes the account from public totals.</summary>
    /// <param name="state">The persisted account state.</param>
    /// <param name="eligible">The expected inclusion.</param>
    [TestCase(AccountState.Normal, true)]
    [TestCase(AccountState.Banned, false)]
    [TestCase(AccountState.TemporarilyBanned, false)]
    public void OnlyEligibleAccountsAreCounted(AccountState state, bool eligible)
    {
        var account = new Account { LoginName = "fixture", State = state };
        Assert.That(ConfiguredJewelCounter.IsEligible(account), Is.EqualTo(eligible));
    }
}
