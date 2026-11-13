// <copyright file="ItemDurabilityProtectionTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.AttributeSystem;
using HNMO.DataModel;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// Tests the <see cref="Stats.ItemDurabilityProtection"/>, which is set by the Talisman of Item Protection.
/// </summary>
[TestFixture]
public class ItemDurabilityProtectionTest
{
    private const double InitialDurability = 100;

    /// <summary>
    /// Tests that an equipped armor item loses durability without the protection, and keeps it with the protection.
    /// </summary>
    /// <param name="protection">The value of the protection attribute.</param>
    /// <param name="expectedDurability">The expected durability after the decrease.</param>
    [TestCase(0f, InitialDurability - 1)]
    [TestCase(1f, InitialDurability)]
    public async Task ProtectionKeepsArmorDurabilityAsync(float protection, double expectedDurability)
    {
        var player = await CreatePlayerAsync(protection).ConfigureAwait(false);
        var armor = CreateItem(InventoryConstants.ArmorSlot);

        await player.DecreaseItemDurabilityAsync(armor, 1).ConfigureAwait(false);

        Assert.That(armor.Durability, Is.EqualTo(expectedDurability));
    }

    /// <summary>
    /// Tests that the protection doesn't apply to the pet, as the client describes it.
    /// </summary>
    [Test]
    public async Task ProtectionDoesNotKeepPetDurabilityAsync()
    {
        var player = await CreatePlayerAsync(1).ConfigureAwait(false);
        var pet = CreateItem(InventoryConstants.PetSlot);

        await player.DecreaseItemDurabilityAsync(pet, 1).ConfigureAwait(false);

        Assert.That(pet.Durability, Is.EqualTo(InitialDurability - 1));
    }

    private static async ValueTask<Player> CreatePlayerAsync(float protection)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        if (protection > 0)
        {
            player.Attributes!.AddElement(new SimpleElement(protection, AggregateType.AddRaw), Stats.ItemDurabilityProtection);
        }

        return player;
    }

    private static Item CreateItem(byte slot)
    {
        var definition = new BasicModel.ItemDefinition { Durability = 255, Width = 2, Height = 2 };
        return new BasicModel.Item { Definition = definition, ItemSlot = slot, Durability = InitialDurability };
    }
}
