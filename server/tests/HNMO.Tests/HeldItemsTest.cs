// <copyright file="HeldItemsTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Text.Json;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.Escrow;
using HNMO.GameLogic.PlugIns.ItemMarket;

/// <summary>
/// The market and escrow keep the real item: it leaves the seller's bag into a storage of its own and reaches the
/// buyer as the same item, every detail kept, once.
/// </summary>
[TestFixture]
public sealed class HeldItemsTest
{
    /// <summary>
    /// A socketed item held by one player is given to another as the very same item; asking again finds nothing.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task TheHeldItemReachesTheBuyerOnceWithItsDetailsAsync()
    {
        var context = GameContextTestHelper.CreateGameContext([]);
        var seller = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        var buyer = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        var item = NewItem();
        item.SocketCount = 3;
        item.PetExperience = 1234;
        await seller.Inventory!.AddItemAsync(20, item).ConfigureAwait(false);

        var held = await HeldItems.HoldAsync(seller, item).ConfigureAwait(false);
        Assert.That(held, Is.Not.Null);
        Assert.That(held!.Id, Is.Not.EqualTo(Guid.Empty));
        Assert.That(seller.Inventory.GetItem(20), Is.Null, "the item left the bag");
        Assert.That(held.Storage.Items, Does.Contain(item));
        Assert.That(await seller.SaveProgressAsync().ConfigureAwait(false), Is.True);

        var given = await HeldItems.GiveAsync(buyer, held.Id).ConfigureAwait(false);
        Assert.That(given, Is.EqualTo(HeldItems.GiveResult.Given));
        var received = buyer.Inventory!.Items.Single();
        Assert.That(received.SocketCount, Is.EqualTo(3));
        Assert.That(received.PetExperience, Is.EqualTo(1234));
        Assert.That(received.Definition, Is.SameAs(item.Definition));

        Assert.That(await HeldItems.GiveAsync(buyer, held.Id).ConfigureAwait(false), Is.EqualTo(HeldItems.GiveResult.Gone));
        Assert.That(buyer.Inventory.Items.Count(), Is.EqualTo(1), "given once");
    }

    /// <summary>An item that couldn't be listed goes back to its slot.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task AReleasedItemIsBackInItsSlotAsync()
    {
        var context = GameContextTestHelper.CreateGameContext([]);
        var seller = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        var item = NewItem();
        await seller.Inventory!.AddItemAsync(25, item).ConfigureAwait(false);

        var held = await HeldItems.HoldAsync(seller, item).ConfigureAwait(false);
        await HeldItems.ReleaseAsync(seller, held!).ConfigureAwait(false);

        Assert.That(seller.Inventory.GetItem(25), Is.SameAs(item));
        Assert.That(held!.Storage.Items, Is.Empty);
    }

    /// <summary>The storage id is kept in the market's and escrow's saved records.</summary>
    [Test]
    public void TheStorageIdIsSaved()
    {
        var id = Guid.NewGuid();
        var market = JsonSerializer.Deserialize<MarketItemSnapshot>(JsonSerializer.Serialize(new MarketItemSnapshot { StorageId = id }));
        var escrow = JsonSerializer.Deserialize<EscrowItemSnapshot>(JsonSerializer.Serialize(new EscrowItemSnapshot { StorageId = id }));
        Assert.That(market!.StorageId, Is.EqualTo(id));
        Assert.That(escrow!.StorageId, Is.EqualTo(id));

        // Records saved before have none: their item is made again from the record.
        Assert.That(JsonSerializer.Deserialize<MarketItemSnapshot>("{\"Group\":0}")!.StorageId, Is.EqualTo(Guid.Empty));
    }

    private static Item NewItem()
    {
        var definition = new HNMO.Persistence.BasicModel.ItemDefinition { Group = 0, Number = 1, Name = "Test sword", Width = 1, Height = 1, Durability = 20 };
        return new HNMO.Persistence.BasicModel.Item { Definition = definition, Durability = 20 };
    }
}
