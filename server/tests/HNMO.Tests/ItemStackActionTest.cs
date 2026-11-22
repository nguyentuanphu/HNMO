// <copyright file="ItemStackActionTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlayerActions.Items;

/// <summary>
/// Tests for <see cref="ItemStackAction"/>.
/// </summary>
[TestFixture]
public class ItemStackActionTest
{
    /// <summary>
    /// Verifies that unstacking jewels with insufficient inventory space does not charge the dismantle fee.
    /// </summary>
    [Test]
    public async ValueTask UnstackWithoutFreeSlotsDoesNotChargeMoneyAsync()
    {
        var context = GameContextTestHelper.CreateGameContext([]);
        var player = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        var singleBless = new HNMO.Persistence.BasicModel.ItemDefinition { Group = 14, Number = 13, Width = 1, Height = 1, Durability = 1 };
        var mixedBless = new HNMO.Persistence.BasicModel.ItemDefinition { Group = 12, Number = 30, Width = 1, Height = 1, Durability = 1 };
        var fillerDef = new HNMO.Persistence.BasicModel.ItemDefinition { Group = 14, Number = 0, Width = 1, Height = 1, Durability = 1 };

        context.Configuration.Items.Add(singleBless);
        context.Configuration.Items.Add(mixedBless);
        context.Configuration.Items.Add(fillerDef);
        context.Configuration.JewelMixes.Add(new HNMO.Persistence.BasicModel.JewelMix
        {
            Number = 0,
            SingleJewel = singleBless,
            MixedJewel = mixedBless,
        });

        var lahapDef = new HNMO.Persistence.BasicModel.MonsterDefinition
        {
            Number = 256,
            Designation = "Lahap",
            NpcWindow = NpcWindow.Lahap,
        };
        var spawnArea = new HNMO.Persistence.BasicModel.MonsterSpawnArea { X1 = 10, X2 = 10, Y1 = 10, Y2 = 10 };
        player.OpenedNpc = new NonPlayerCharacter(spawnArea, lahapDef, player.CurrentMap!);

        var stacked = player.PersistenceContext.CreateNew<HNMO.DataModel.Entities.Item>();
        stacked.Definition = mixedBless;
        stacked.Level = 0; // 10 jewels
        stacked.Durability = 1;
        await player.Inventory!.AddItemAsync(InventoryConstants.EquippableSlotsCount, stacked).ConfigureAwait(false);

        // Leave fewer than 10 free slots in the base 64-slot inventory (slots 12..75).
        for (byte slot = (byte)(InventoryConstants.EquippableSlotsCount + 1); slot < InventoryConstants.EquippableSlotsCount + 56; slot++)
        {
            var filler = player.PersistenceContext.CreateNew<HNMO.DataModel.Entities.Item>();
            filler.Definition = fillerDef;
            filler.Durability = 1;
            await player.Inventory.AddItemAsync(slot, filler).ConfigureAwait(false);
        }

        player.TryAddMoney(2_000_000);
        await new ItemStackAction().UnstackItemsAsync(player, 0, InventoryConstants.EquippableSlotsCount).ConfigureAwait(false);

        Assert.That(player.Money, Is.EqualTo(2_000_000), "Unstacking without enough free slots must not deduct the dismantle fee.");
        Assert.That(player.Inventory.GetItem(InventoryConstants.EquippableSlotsCount), Is.SameAs(stacked));
    }
}
