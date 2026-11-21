// <copyright file="StoreBuyViewRangeTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.PlayerStore;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// A personal store can only be browsed and bought from by a buyer who sees it, i.e. within the buyer's view range (owner, 21 Nov 2026).
/// </summary>
[TestFixture]
public class StoreBuyViewRangeTest
{
    private const int Price = 1500;
    private const int Money = 50000;

    /// <summary>A buyer who doesn't see the store can't request its item list or buy from it.</summary>
    [Test]
    public async Task BuyerOutOfViewRangeCantBrowseOrBuyAsync()
    {
        var (buyer, seller, item) = await CreateStoreAsync().ConfigureAwait(false);
        await seller.RemoveObserverAsync(buyer).ConfigureAwait(false);

        await new StoreItemListRequestAction().RequestStoreItemListAsync(buyer, seller).ConfigureAwait(false);
        await new BuyRequestAction().BuyItemAsync(buyer, seller, InventoryConstants.FirstStoreItemSlotIndex).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(buyer.LastRequestedPlayerStore, Is.Null, "store list not opened out of view range");
            Assert.That(seller.ShopStorage!.GetItem(InventoryConstants.FirstStoreItemSlotIndex), Is.SameAs(item), "still in the store");
            Assert.That(buyer.Money, Is.EqualTo(Money));
        });
    }

    /// <summary>A buyer who sees the store can browse and buy from it.</summary>
    [Test]
    public async Task BuyerInViewRangeBrowsesAndBuysAsync()
    {
        var (buyer, seller, item) = await CreateStoreAsync().ConfigureAwait(false);
        await seller.AddObserverAsync(buyer).ConfigureAwait(false);

        await new StoreItemListRequestAction().RequestStoreItemListAsync(buyer, seller).ConfigureAwait(false);
        await new BuyRequestAction().BuyItemAsync(buyer, seller, InventoryConstants.FirstStoreItemSlotIndex).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(buyer.LastRequestedPlayerStore, Is.Not.Null, "store list opened in view range");
            Assert.That(seller.ShopStorage!.GetItem(InventoryConstants.FirstStoreItemSlotIndex), Is.Null, "sold");
            Assert.That(buyer.Money, Is.EqualTo(Money - Price));
        });
    }

    private static async Task<(Player Buyer, Player Seller, BasicModel.Item Item)> CreateStoreAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var seller = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        var buyer = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        buyer.Money = Money;
        var definition = new BasicModel.ItemDefinition
        {
            Name = "Kris",
            Width = 1,
            Height = 2,
            Durability = 10,
            IsDroppable = true,
            IsTradable = true,
            IsPersonalStoreSellable = true,
            IsStorable = true,
        };
        var item = new BasicModel.Item
        {
            Definition = definition,
            ItemSlot = InventoryConstants.FirstStoreItemSlotIndex,
            Durability = 1,
            StorePrice = Price,
        };
        Assert.That(await seller.ShopStorage!.AddItemAsync(InventoryConstants.FirstStoreItemSlotIndex, item).ConfigureAwait(false), Is.True);
        seller.ShopStorage.StoreOpen = true;
        return (buyer, seller, item);
    }
}
