// <copyright file="PersonalStoreBuySpaceRaceTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.PlayerStore;

/// <summary>
/// Two buys at two personal stores at the same time, with one free slot in the buyer's inventory:
/// the second buy must not take the money and lose the item.
/// </summary>
[TestFixture]
public class PersonalStoreBuySpaceRaceTest
{
    private const int Price = 100;

    /// <summary>
    /// The second buy checked the space before the first buy filled the last slot; it is refused under the store lock.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SecondBuyIsRefusedWhenTheLastSlotWasTakenMeanwhileAsync()
    {
        var context = GameContextTestHelper.CreateGameContext([]);
        var buyer = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        var firstSeller = await PlayerTestHelper.CreatePlayerAsync(buyer.GameContext).ConfigureAwait(false);
        var secondSeller = await PlayerTestHelper.CreatePlayerAsync(buyer.GameContext).ConfigureAwait(false);
        buyer.Money = 1_000_000;
        await FillInventoryButOneSlotAsync(buyer).ConfigureAwait(false);

        var firstItem = await PutIntoStoreAsync(firstSeller).ConfigureAwait(false);
        var secondItem = await PutIntoStoreAsync(secondSeller).ConfigureAwait(false);
        var action = new BuyRequestAction();

        Task secondBuy;
        using (await secondSeller.ShopStorage!.StoreLock.LockAsync().ConfigureAwait(false))
        {
            // The second buy checks the inventory (one slot free) and waits for its store's lock ...
            secondBuy = action.BuyItemAsync(buyer, secondSeller, InventoryConstants.FirstStoreItemSlotIndex).AsTask();

            // ... while the first buy takes the last free slot.
            await action.BuyItemAsync(buyer, firstSeller, InventoryConstants.FirstStoreItemSlotIndex).ConfigureAwait(false);
        }

        await secondBuy.ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(buyer.Inventory!.Items.Any(i => ReferenceEquals(i, firstItem)), Is.True, "the first item is in the inventory");
            Assert.That(secondSeller.ShopStorage.GetItem(InventoryConstants.FirstStoreItemSlotIndex), Is.SameAs(secondItem), "the second item stays in the store");
            Assert.That(buyer.Money, Is.EqualTo(1_000_000 - Price), "only the first item is paid");
            Assert.That(secondSeller.Money, Is.EqualTo(0), "the second seller gets nothing");
        });
    }

    private static async ValueTask FillInventoryButOneSlotAsync(Player player)
    {
        Item? last = null;
        while (player.Inventory!.CheckInvSpace(CreateItem()) is not null)
        {
            last = CreateItem();
            await player.Inventory.AddItemAsync(last).ConfigureAwait(false);
        }

        await player.Inventory.RemoveItemAsync(last!).ConfigureAwait(false);
    }

    private static async ValueTask<Item> PutIntoStoreAsync(Player seller)
    {
        var item = CreateItem();
        item.StorePrice = Price;
        await seller.ShopStorage!.AddItemAsync(InventoryConstants.FirstStoreItemSlotIndex, item).ConfigureAwait(false);
        seller.ShopStorage!.StoreOpen = true;
        return item;
    }

    private static Item CreateItem()
    {
        var definition = new HNMO.Persistence.BasicModel.ItemDefinition
        {
            Name = "Small Axe",
            Width = 1,
            Height = 1,
            Durability = 10,
            IsDroppable = true,
            IsTradable = true,
            IsPersonalStoreSellable = true,
            IsStorable = true,
        };
        return new HNMO.Persistence.BasicModel.Item { Id = Guid.NewGuid(), Definition = definition, Durability = 1 };
    }
}
