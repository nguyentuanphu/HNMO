// <copyright file="StoreBuyViewRangeTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.PlayerStore;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// A personal store sells only to a buyer who sees it, i.e. within the buyer's view range (owner, 21 Nov 2026).
/// </summary>
[TestFixture]
public class StoreBuyViewRangeTest
{
    private const int Price = 1500;
    private const int Money = 50000;

    /// <summary>A buyer who doesn't see the store can't buy.</summary>
    [Test]
    public async Task BuyerOutOfViewRangeCantBuyAsync()
    {
        var (buyer, seller, item) = await CreateStoreAsync().ConfigureAwait(false);
        await seller.RemoveObserverAsync(buyer).ConfigureAwait(false);

        await new BuyRequestAction().BuyItemAsync(buyer, seller, InventoryConstants.FirstStoreItemSlotIndex).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(seller.ShopStorage!.GetItem(InventoryConstants.FirstStoreItemSlotIndex), Is.SameAs(item), "still in the store");
            Assert.That(buyer.Money, Is.EqualTo(Money));
        });
    }

    /// <summary>A buyer who sees the store buys.</summary>
    [Test]
    public async Task BuyerInViewRangeBuysAsync()
    {
        var (buyer, seller, item) = await CreateStoreAsync().ConfigureAwait(false);
        await seller.AddObserverAsync(buyer).ConfigureAwait(false);

        await new BuyRequestAction().BuyItemAsync(buyer, seller, InventoryConstants.FirstStoreItemSlotIndex).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(seller.ShopStorage!.GetItem(InventoryConstants.FirstStoreItemSlotIndex), Is.Null, "sold");
            Assert.That(buyer.Money, Is.EqualTo(Money - Price));
        });
    }

    private static async Task<(Player Buyer, Player Seller, BasicModel.Item Item)> CreateStoreAsync()
    {
        var seller = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var buyer = await PlayerTestHelper.CreatePlayerAsync(seller.GameContext).ConfigureAwait(false);
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
        var item = new BasicModel.Item { Id = Guid.NewGuid(), Definition = definition, Durability = 1, StorePrice = Price };
        Assert.That(await seller.ShopStorage!.AddItemAsync(InventoryConstants.FirstStoreItemSlotIndex, item).ConfigureAwait(false), Is.True);
        seller.ShopStorage.StoreOpen = true;
        return (buyer, seller, item);
    }
}
