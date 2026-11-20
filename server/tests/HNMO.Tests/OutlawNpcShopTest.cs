// <copyright file="OutlawNpcShopTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using Moq;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlayerActions;
using HNMO.GameLogic.PlayerActions.Items;

/// <summary>
/// Tests that outlaws from PK level 2 up can't use the NPC shops (open a merchant, buy, sell, repair at an NPC),
/// while the Warning stage (one kill) still may.
/// </summary>
[TestFixture]
public class OutlawNpcShopTest
{
    private const byte StoreSlot = 5;
    private const byte InventorySlot = 20;
    private const int Money = 1_000_000;

    /// <summary>
    /// Opening a merchant.
    /// </summary>
    /// <param name="state">The hero state.</param>
    /// <param name="allowed">Whether the shop may be used.</param>
    /// <returns>The task.</returns>
    [TestCase(HeroState.Normal, true)]
    [TestCase(HeroState.PlayerKillWarning, true)]
    [TestCase(HeroState.PlayerKiller1stStage, false)]
    [TestCase(HeroState.PlayerKiller2ndStage, false)]
    public async Task OpenMerchantAsync(HeroState state, bool allowed)
    {
        var player = await CreatePlayerAsync(state).ConfigureAwait(false);
        var merchant = CreateMerchant(CreateStoreItem());

        await new TalkNpcAction().TalkToNpcAsync(player, merchant).ConfigureAwait(false);

        Assert.That(player.OpenedNpc, allowed ? Is.SameAs(merchant) : Is.Null);
    }

    /// <summary>
    /// Buying from a merchant.
    /// </summary>
    /// <param name="state">The hero state.</param>
    /// <param name="allowed">Whether the shop may be used.</param>
    /// <returns>The task.</returns>
    [TestCase(HeroState.PlayerKillWarning, true)]
    [TestCase(HeroState.PlayerKiller1stStage, false)]
    [TestCase(HeroState.PlayerKiller2ndStage, false)]
    public async Task BuyAsync(HeroState state, bool allowed)
    {
        var player = await CreatePlayerAsync(state).ConfigureAwait(false);
        player.OpenedNpc = CreateMerchant(CreateStoreItem());

        await new BuyNpcItemAction().BuyItemAsync(player, StoreSlot).ConfigureAwait(false);

        Assert.That(player.Money, allowed ? Is.LessThan(Money) : Is.EqualTo(Money));
    }

    /// <summary>
    /// Selling to a merchant.
    /// </summary>
    /// <param name="state">The hero state.</param>
    /// <param name="allowed">Whether the shop may be used.</param>
    /// <returns>The task.</returns>
    [TestCase(HeroState.PlayerKillWarning, true)]
    [TestCase(HeroState.PlayerKiller1stStage, false)]
    [TestCase(HeroState.PlayerKiller2ndStage, false)]
    public async Task SellAsync(HeroState state, bool allowed)
    {
        var player = await CreatePlayerAsync(state).ConfigureAwait(false);
        player.OpenedNpc = CreateMerchant(CreateStoreItem());
        player.GameContext.Configuration.MaximumInventoryMoney = int.MaxValue;
        var item = CreateMockedItem(definition => definition.IsSellableToNpc = true);
        await player.Inventory!.AddItemAsync(InventorySlot, item).ConfigureAwait(false);

        var sold = await new SellItemToNpcAction().SellItemAsync(player, InventorySlot).ConfigureAwait(false);

        Assert.That(sold, Is.EqualTo(allowed));
        Assert.That(player.Inventory.GetItem(InventorySlot), allowed ? Is.Null : Is.SameAs(item));
    }

    /// <summary>
    /// Repairing at an NPC; the repair in the inventory, without an NPC, stays possible.
    /// </summary>
    /// <param name="state">The hero state.</param>
    /// <param name="atNpc">Whether the repair is at an NPC.</param>
    /// <param name="allowed">Whether the repair is done.</param>
    /// <returns>The task.</returns>
    [TestCase(HeroState.PlayerKillWarning, true, true)]
    [TestCase(HeroState.PlayerKiller1stStage, true, false)]
    [TestCase(HeroState.PlayerKiller2ndStage, true, false)]
    [TestCase(HeroState.PlayerKiller2ndStage, false, true)]
    public async Task RepairAsync(HeroState state, bool atNpc, bool allowed)
    {
        var player = await CreatePlayerAsync(state).ConfigureAwait(false);
        if (atNpc)
        {
            player.OpenedNpc = CreateMerchant(CreateStoreItem());
        }

        var item = CreateMockedItem(definition =>
        {
            definition.ItemSlot = new ItemSlotType();
            definition.IsRepairable = true;
        });
        item.Durability = 5;
        await player.Inventory!.AddItemAsync(InventorySlot, item).ConfigureAwait(false);

        await new ItemRepairAction().RepairItemAsync(player, InventorySlot).ConfigureAwait(false);

        Assert.That(item.Durability, Is.EqualTo(allowed ? 10 : 5));
    }

    private static async ValueTask<Player> CreatePlayerAsync(HeroState state)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.SelectedCharacter!.State = state;
        player.SelectedCharacter.StateRemainingSeconds = state > HeroState.Normal ? 50_000 : 0;
        player.Money = Money;
        return player;
    }

    private static NonPlayerCharacter CreateMerchant(Item storeItem)
    {
        var store = new Mock<ItemStorage>();
        store.SetupAllProperties();
        store.Setup(s => s.Items).Returns(new List<Item> { storeItem });
        return new NonPlayerCharacter(null!, new MonsterDefinition { MerchantStore = store.Object }, null!);
    }

    private static Item CreateStoreItem()
    {
        var item = CreateItem(CreateDefinition(), 1);
        item.ItemSlot = StoreSlot;
        return item;
    }

    private static ItemDefinition CreateDefinition()
    {
        return new ItemDefinition
        {
            Group = 14,
            Value = 500,

            // Like the scrolls and orbs of the initialized data, which cost their value.
            PriceDefinition = new HNMO.Persistence.BasicModel.ItemPriceDefinition { BasePriceFormula = "value" },
            Width = 1,
            Height = 1,
            Durability = 10,
        };
    }

    private static Item CreateMockedItem(Action<ItemDefinition> setRule)
    {
        var definitionMock = new Mock<ItemDefinition>();
        definitionMock.SetupAllProperties();
        definitionMock.Setup(d => d.BasePowerUpAttributes).Returns(new List<ItemBasePowerUpDefinition>());
        var definition = definitionMock.Object;
        definition.Width = 1;
        definition.Height = 1;
        definition.Durability = 10;
        setRule(definition);
        return CreateItem(definition, 1);
    }

    private static Item CreateItem(ItemDefinition definition, double durability)
    {
        var item = new Mock<Item>();
        item.SetupAllProperties();
        item.Setup(i => i.ItemOptions).Returns(new List<ItemOptionLink>());
        item.Setup(i => i.ItemSetGroups).Returns(new List<ItemOfItemSet>());
        item.Object.Definition = definition;
        item.Object.Durability = durability;
        return item.Object;
    }
}
