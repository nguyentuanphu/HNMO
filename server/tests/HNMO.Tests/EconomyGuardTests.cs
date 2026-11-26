// <copyright file="EconomyGuardTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.GameLogic.PlugIns.CombiningLeftovers;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// Combining leftovers counts the durability of a stack as its pieces, but never the wear of a piece of equipment.
/// </summary>
[TestFixture]
public class EconomyGuardTests
{
    /// <summary>A weapon with durability 30 is one piece for a recipe; a stack of 7 is seven.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async ValueTask EquipmentCountsAsOnePieceAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync(GameContextTestHelper.CreateGameContext()).ConfigureAwait(false);
        var sword = new BasicModel.Item { Definition = Sword(), Durability = 30 };
        var renas = new BasicModel.Item { Definition = new BasicModel.ItemDefinition { Group = 14, Number = 21, Width = 1, Height = 1, Durability = 255 }, Durability = 7 };
        await player.Inventory!.AddItemAsync(12, sword).ConfigureAwait(false);
        await player.Inventory.AddItemAsync(14, renas).ConfigureAwait(false);

        var bag = CombiningLeftoversStateWriter.ReadBag(player);

        Assert.That(bag.Single(b => b.Slot == 12).Quantity, Is.EqualTo(1));
        Assert.That(bag.Single(b => b.Slot == 14).Quantity, Is.EqualTo(7));
    }

    /// <summary>Consuming a weapon removes it; its durability is not lowered instead.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async ValueTask ConsumedEquipmentIsRemovedAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync(GameContextTestHelper.CreateGameContext()).ConfigureAwait(false);
        var sword = new BasicModel.Item { Definition = Sword(), Durability = 30 };
        await player.Inventory!.AddItemAsync(12, sword).ConfigureAwait(false);

        await CombiningLeftoversPlugIn.RemoveBagSlotsAsync(player, [12]).ConfigureAwait(false);

        Assert.That(player.Inventory.GetItem(12), Is.Null);
    }

    /// <summary>Consuming two pieces of a stack of five leaves three.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async ValueTask ConsumedStackPiecesLowerTheStackAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync(GameContextTestHelper.CreateGameContext()).ConfigureAwait(false);
        var jewels = new BasicModel.Item { Definition = new BasicModel.ItemDefinition { Group = 14, Number = 16, Width = 1, Height = 1, Durability = 255 }, Durability = 5 };
        await player.Inventory!.AddItemAsync(12, jewels).ConfigureAwait(false);

        await CombiningLeftoversPlugIn.RemoveBagSlotsAsync(player, [12, 12]).ConfigureAwait(false);

        Assert.That(player.Inventory.GetItem(12), Is.SameAs(jewels));
        Assert.That(jewels.Durability, Is.EqualTo(3));
    }

    private static BasicModel.ItemDefinition Sword()
        => new() { Group = 0, Number = 0, Width = 1, Height = 3, Durability = 30, ItemSlot = new BasicModel.ItemSlotType() };
}
