// <copyright file="InventoryBackupRollbackTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Items;
using HNMO.GameLogic.PlayerActions.PlayerStore;
using HNMO.GameLogic.PlayerActions.Trade;
using HNMO.Pathfinding;

/// <summary>
/// While a trade (or a crafting window) is open, the inventory has a backup, and a cancel rolls the
/// inventory back to it. Changes of the inventory in the meantime must not be possible, otherwise the
/// rollback duplicates (or loses) what changed: a dropped item comes back while it lies on the ground,
/// a stacked item comes back while the target keeps its pieces, a sold store item comes back to the seller.
/// </summary>
[TestFixture]
public class InventoryBackupRollbackTest
{
    private const byte BagSlot = 20;

    /// <summary>
    /// An item can't be dropped while a trade is open, so cancelling the trade can't give it back
    /// while it lies on the ground.
    /// </summary>
    [Test]
    public async ValueTask DropDuringTradeIsRefusedAsync()
    {
        var (player, partner) = await OpenTradeAsync().ConfigureAwait(false);
        var item = CreateItem(durability: 1);
        await player.Inventory!.AddItemAsync(BagSlot, item).ConfigureAwait(false);
        player.BackupInventory = new BackupItemStorage(player.Inventory.ItemStorage);

        await new DropItemAction().DropItemAsync(player, BagSlot, player.Position).ConfigureAwait(false);
        await new TradeCancelAction().CancelTradeAsync(player).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(player.CurrentMap!.GetDropsInRange(player.Position, 5).OfType<DroppedItem>(), Is.Empty, "dropped");
            Assert.That(player.Inventory.Items, Does.Contain(item));
            Assert.That(partner.TradingPartner, Is.Null);
        });
    }

    /// <summary>
    /// A stack can't be put completely onto another one while a trade is open: the cancel would bring
    /// the moved stack back while the target keeps its pieces.
    /// </summary>
    [Test]
    public async ValueTask CompleteStackDuringTradeIsRefusedAsync()
    {
        var (player, _) = await OpenTradeAsync().ConfigureAwait(false);
        var definition = CreateStackableDefinition();
        var moved = CreateItem(durability: 5, definition);
        var target = CreateItem(durability: 5, definition);
        await player.Inventory!.AddItemAsync(BagSlot, moved).ConfigureAwait(false);
        await player.Inventory.AddItemAsync(BagSlot + 1, target).ConfigureAwait(false);
        player.BackupInventory = new BackupItemStorage(player.Inventory.ItemStorage);

        await new MoveItemAction().MoveItemAsync(player, BagSlot, Storages.Inventory, BagSlot + 1, Storages.Inventory).ConfigureAwait(false);
        await new TradeCancelAction().CancelTradeAsync(player).ConfigureAwait(false);

        var pieces = player.Inventory.Items.Where(i => i.Definition == definition).Sum(i => i.Durability);
        Assert.That(pieces, Is.EqualTo(10), "pieces after the cancelled trade");
    }

    /// <summary>
    /// A complete stack outside of a trade still works.
    /// </summary>
    [Test]
    public async ValueTask CompleteStackOutsideOfTradeWorksAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var definition = CreateStackableDefinition();
        var moved = CreateItem(durability: 5, definition);
        var target = CreateItem(durability: 5, definition);
        await player.Inventory!.AddItemAsync(BagSlot, moved).ConfigureAwait(false);
        await player.Inventory.AddItemAsync(BagSlot + 1, target).ConfigureAwait(false);

        await new MoveItemAction().MoveItemAsync(player, BagSlot, Storages.Inventory, BagSlot + 1, Storages.Inventory).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(target.Durability, Is.EqualTo(10));
            Assert.That(player.Inventory.Items, Does.Not.Contain(moved));
        });
    }

    /// <summary>
    /// A store item can't be bought while the seller has a trade open: cancelling it would give the
    /// sold item back to the seller.
    /// </summary>
    [Test]
    public async ValueTask StorePurchaseWhileTheSellerTradesIsRefusedAsync()
    {
        var (seller, _) = await OpenTradeAsync().ConfigureAwait(false);
        var buyer = await PlayerTestHelper.CreatePlayerAsync(seller.GameContext).ConfigureAwait(false);
        buyer.Money = 50000;
        var storeItem = CreateItem(durability: 1);
        storeItem.StorePrice = 1500;
        Assert.That(await seller.ShopStorage!.AddItemAsync(InventoryConstants.FirstStoreItemSlotIndex, storeItem).ConfigureAwait(false), Is.True);
        seller.ShopStorage.StoreOpen = true;
        seller.BackupInventory = new BackupItemStorage(seller.Inventory!.ItemStorage);

        await new BuyRequestAction().BuyItemAsync(buyer, seller, InventoryConstants.FirstStoreItemSlotIndex).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(buyer.Inventory!.Items, Does.Not.Contain(storeItem));
            Assert.That(buyer.Money, Is.EqualTo(50000));
        });
    }

    private static async ValueTask<(Player Player, Player Partner)> OpenTradeAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var partner = await PlayerTestHelper.CreatePlayerAsync(player.GameContext).ConfigureAwait(false);
        player.Position = new Point(10, 10);
        partner.Position = new Point(11, 10);
        Assert.That(await new TradeRequestAction().RequestTradeAsync(player, partner).ConfigureAwait(false), Is.True);
        await new TradeAcceptAction().HandleTradeAcceptAsync(partner, true).ConfigureAwait(false);
        Assert.That(player.PlayerState.CurrentState, Is.EqualTo(PlayerState.TradeOpened), "test setup");
        return (player, partner);
    }

    private static HNMO.Persistence.BasicModel.ItemDefinition CreateStackableDefinition()
    {
        return new HNMO.Persistence.BasicModel.ItemDefinition
        {
            Name = "Healing Potion",
            Group = 14,
            Number = 1,
            Width = 1,
            Height = 1,
            Durability = 20,
            IsDroppable = true,
            IsTradable = true,
            IsStorable = true,
        };
    }

    private static HNMO.Persistence.BasicModel.Item CreateItem(byte durability, ItemDefinition? definition = null)
    {
        definition ??= new HNMO.Persistence.BasicModel.ItemDefinition
        {
            Name = "Kris",
            Width = 1,
            Height = 2,
            Durability = 10,
            IsDroppable = true,
            IsTradable = true,
            IsPersonalStoreSellable = true,
            IsStorable = true,
            IsSellableToNpc = true,
        };

        return new HNMO.Persistence.BasicModel.Item { Id = Guid.NewGuid(), Definition = definition, Durability = durability };
    }
}
