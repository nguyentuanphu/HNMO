// <copyright file="GiftBoxStorageCacheTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel.Entities;
using HNMO.GameLogic;

/// <summary>Committed mail items enter the live grid without a second persistent insertion or accidental equipment.</summary>
[TestFixture]
public class GiftBoxStorageCacheTests
{
    /// <summary>A staged backing item is invisible to the live grid until explicitly included after confirmation.</summary>
    [Test]
    public void StoredItemIsCachedWithoutAddingAnotherEntity()
    {
        var backing = new HNMO.Persistence.BasicModel.ItemStorage();
        var storage = new Storage(76, 12, 0, backing);
        var item = NewItem(12);
        backing.Items.Add(item);
        Assert.That(storage.Items, Is.Empty);
        Assert.That(storage.TryIncludeStoredBagItem(item), Is.True);
        Assert.That(storage.TryIncludeStoredBagItem(item), Is.True);
        Assert.That(storage.Items.Single(), Is.SameAs(item));
        Assert.That(backing.Items, Has.Count.EqualTo(1));
        Assert.That(storage.CheckInvSpace(NewItem(0)), Is.EqualTo(13));
    }

    /// <summary>Items from another context and occupied slots cannot poison a live storage cache.</summary>
    [Test]
    public void ForeignOrConflictingItemsAreRefused()
    {
        var backing = new HNMO.Persistence.BasicModel.ItemStorage();
        var storage = new Storage(76, 12, 0, backing);
        var original = NewItem(12);
        backing.Items.Add(original);
        Assert.That(storage.TryIncludeStoredBagItem(original), Is.True);
        Assert.That(storage.TryIncludeStoredBagItem(NewItem(13)), Is.False);
        var conflicting = NewItem(12);
        backing.Items.Add(conflicting);
        Assert.That(storage.TryIncludeStoredBagItem(conflicting), Is.False);
        Assert.That(storage.Items.Single(), Is.SameAs(original));
        Assert.That(backing.Items, Has.Count.EqualTo(2));
    }

    /// <summary>The helper cannot equip an item while bypassing power-up/equipment change hooks.</summary>
    [Test]
    public void EquipmentSlotsAreRefused()
    {
        var backing = new HNMO.Persistence.BasicModel.ItemStorage();
        var storage = new Storage(76, 12, 0, backing);
        var item = NewItem(0);
        backing.Items.Add(item);
        Assert.That(storage.TryIncludeStoredBagItem(item), Is.False);
        Assert.That(storage.Items, Is.Empty);
        Assert.That(backing.Items.Single(), Is.SameAs(item));
    }

    /// <summary>Committed extension items update that grid while remaining one persistent object.</summary>
    [Test]
    public void ExtensionCacheIncludesItsOwnStoredSlot()
    {
        var backing = new HNMO.Persistence.BasicModel.ItemStorage();
        var storage = new ExtendedStorage(backing);
        var item = NewItem(76);
        backing.Items.Add(item);
        Assert.That(storage.TryIncludeStoredBagItem(item), Is.True);
        Assert.That(storage.Items.Single(), Is.SameAs(item));
        Assert.That(storage.Extensions.Single().CheckInvSpace(NewItem(0)), Is.EqualTo(77));
        Assert.That(backing.Items, Has.Count.EqualTo(1));
    }

    private static Item NewItem(byte slot) => new HNMO.Persistence.BasicModel.Item
    {
        Id = Guid.NewGuid(),
        ItemSlot = slot,
        Definition = new HNMO.Persistence.BasicModel.ItemDefinition { Width = 1, Height = 1 },
    };

    private sealed class ExtendedStorage : Storage
    {
        public ExtendedStorage(ItemStorage backing)
            : base(76, 12, 0, backing)
        {
            this.Extensions = [new Storage(32, 0, 76, backing)];
        }
    }
}
