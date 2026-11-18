// <copyright file="GiftBoxPackingTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.GiftBox;
using HNMO.GameLogic.PlugIns.GiftCodes;

/// <summary>Real inventory-grid preflight preserves owed rewards and never mutates the player's bag.</summary>
[TestFixture]
public class GiftBoxPackingTests
{
    /// <summary>A reward which only partly fits leaves every real item and the owed letter unchanged.</summary>
    [Test]
    public async Task PartialFitLeavesTheWholeLetterOwedAsync()
    {
        var (configuration, definition) = Configuration();
        var real = Enumerable.Range(12, 63).Select(slot => new TemporaryItem { Definition = definition, ItemSlot = (byte)slot }).ToArray();
        var before = real.Select(item => item.ItemSlot).ToArray();
        var reward = new GiftCodeReward { Items = [new() { Count = 2 }] };
        var account = Guid.NewGuid();
        var box = new GiftBoxDispatch(new HNMO.Persistence.InMemoryHnmoPlayerStateRowBackend());
        box.Send(Guid.NewGuid(), "host", "event compensation", [account], "Gift", "Quà", "For joining", "Cảm ơn bạn tham gia", reward, new(2026, 11, 18, 6, 0, 0, DateTimeKind.Utc));
        var result = await GiftBoxPacking.PlanAsync(configuration, real, 0, box.Read(account).Single().Reward).ConfigureAwait(false);
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(GiftBoxPackingStatus.BagFull));
            Assert.That(result.Items, Is.Empty);
            Assert.That(real.Select(item => item.ItemSlot), Is.EqualTo(before));
            Assert.That(box.Read(account).Single().Reward.Items.Single().Count, Is.EqualTo(2));
        });
    }

    /// <summary>A full main bag uses an unlocked extension, without putting items in equipment slots.</summary>
    [Test]
    public async Task UnlockedExtensionReceivesAPlannedItemAsync()
    {
        var (configuration, definition) = Configuration();
        var real = Enumerable.Range(12, 64).Select(slot => new TemporaryItem { Definition = definition, ItemSlot = (byte)slot }).ToArray();
        var result = await GiftBoxPacking.PlanAsync(configuration, real, 1, new() { Items = [new() { Level = 3 }] }).ConfigureAwait(false);
        Assert.That(result.Status, Is.EqualTo(GiftBoxPackingStatus.Ready));
        Assert.That(result.Items.Single().ItemSlot, Is.EqualTo(InventoryConstants.FirstExtensionItemSlotIndex));
        Assert.That(result.Items.Single().Level, Is.EqualTo(3));
        Assert.That(real, Has.Length.EqualTo(64));
    }

    /// <summary>An item cannot span two separate extension grids even when their combined area is large enough.</summary>
    [Test]
    public async Task ExtensionBoundariesAreRespectedAsync()
    {
        var (configuration, definition) = Configuration();
        var real = Enumerable.Range(12, 64).Select(slot => new TemporaryItem { Definition = definition, ItemSlot = (byte)slot }).ToArray();
        configuration.Items.Add(new HNMO.Persistence.BasicModel.ItemDefinition { Group = 0, Number = 1, Width = 1, Height = 5, MaximumItemLevel = 15, Durability = 100 });
        var result = await GiftBoxPacking.PlanAsync(configuration, real, 2, new() { Items = [new() { Number = 1 }] }).ConfigureAwait(false);
        Assert.That(result.Status, Is.EqualTo(GiftBoxPackingStatus.BagFull));
        Assert.That(result.Items, Is.Empty);
    }

    /// <summary>Unsupported options or removed definitions are refused instead of silently dropping the promise.</summary>
    [Test]
    public async Task InvalidItemsDoNotBecomeReducedRewardsAsync()
    {
        var (configuration, _) = Configuration();
        foreach (var item in new GiftCodeItem[] { new() { Skill = true }, new() { Luck = true }, new() { ExcellentOptions = 1 }, new() { Level = 16 }, new() { Number = 511 } })
        {
            var result = await GiftBoxPacking.PlanAsync(configuration, [], 0, new() { Items = [item] }).ConfigureAwait(false);
            Assert.That(result.Status, Is.EqualTo(GiftBoxPackingStatus.InvalidReward));
            Assert.That(result.Items, Is.Empty);
        }
    }

    /// <summary>Preflight does not repair a malformed original inventory as a side effect.</summary>
    [Test]
    public async Task OverlappingOriginalItemsAreNotMovedAsync()
    {
        var (configuration, definition) = Configuration();
        var real = new[] { new TemporaryItem { Definition = definition, ItemSlot = 12 }, new TemporaryItem { Definition = definition, ItemSlot = 12 } };
        var result = await GiftBoxPacking.PlanAsync(configuration, real, 0, new() { Items = [new()] }).ConfigureAwait(false);
        Assert.That(result.Status, Is.EqualTo(GiftBoxPackingStatus.InvalidInventory));
        Assert.That(real.Select(item => item.ItemSlot), Is.EqualTo(new byte[] { 12, 12 }));
    }

    /// <summary>Coins-only mail does not need a bag slot, and personal-store slots do not count as bag contents.</summary>
    [Test]
    public async Task CoinsOnlyAndPersonalStoreContentsAreHandledSeparatelyAsync()
    {
        var (configuration, definition) = Configuration();
        var real = Enumerable.Range(12, 64).Select(slot => new TemporaryItem { Definition = definition, ItemSlot = (byte)slot }).ToArray();
        var coins = await GiftBoxPacking.PlanAsync(configuration, real, 0, new() { Coins = 10 }).ConfigureAwait(false);
        Assert.That(coins.Status, Is.EqualTo(GiftBoxPackingStatus.Ready));
        Assert.That(coins.Items, Is.Empty);
        var personalStore = new TemporaryItem { Definition = definition, ItemSlot = InventoryConstants.FirstStoreItemSlotIndex };
        var goods = await GiftBoxPacking.PlanAsync(configuration, [personalStore], 0, new() { Items = [new()] }).ConfigureAwait(false);
        Assert.That(goods.Status, Is.EqualTo(GiftBoxPackingStatus.Ready));
        Assert.That(goods.Items.Single().ItemSlot, Is.EqualTo(12));
        Assert.That(personalStore.ItemSlot, Is.EqualTo(InventoryConstants.FirstStoreItemSlotIndex));
    }

    private static (HNMO.Persistence.BasicModel.GameConfiguration Configuration, HNMO.Persistence.BasicModel.ItemDefinition Definition) Configuration()
    {
        var configuration = new HNMO.Persistence.BasicModel.GameConfiguration();
        var definition = new HNMO.Persistence.BasicModel.ItemDefinition { Group = 0, Number = 0, Width = 1, Height = 1, MaximumItemLevel = 15, Durability = 100 };
        configuration.Items.Add(definition);
        return (configuration, definition);
    }
}
