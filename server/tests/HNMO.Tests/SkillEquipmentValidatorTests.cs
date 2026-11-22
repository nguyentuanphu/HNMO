// <copyright file="SkillEquipmentValidatorTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlayerActions.Skills;
using HNMO.Persistence;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// Verifies that the server rejects attack skills whose required weapon, glove, bow, or mount
/// rule is not satisfied by the player's equipped items, matching the desktop and web clients.
/// </summary>
[TestFixture]
public class SkillEquipmentValidatorTests
{
    /// <summary>
    /// Impale (47) requires both a spear in the right hand and an active rideable mount.
    /// </summary>
    [Test]
    public async ValueTask ImpaleRequiresSpearAndMountAsync()
    {
        var player = await CreateReadyPlayerAsync().ConfigureAwait(false);
        var impale = new Skill { Number = 47, DamageType = DamageType.Physical };

        Assert.That(await AttackActionValidator.CanAttackAsync(player, impale).ConfigureAwait(false), Is.False, "unarmed and unmounted");

        await EquipAsync(player, InventoryConstants.RightHandSlot, group: 3, number: 0).ConfigureAwait(false); // Spear
        Assert.That(await AttackActionValidator.CanAttackAsync(player, impale).ConfigureAwait(false), Is.False, "spear without mount");

        await EquipAsync(player, InventoryConstants.RightHandSlot, group: 0, number: 0).ConfigureAwait(false); // Sword
        await EquipAsync(player, InventoryConstants.PetSlot, ItemConstants.Uniria).ConfigureAwait(false);
        Assert.That(await AttackActionValidator.CanAttackAsync(player, impale).ConfigureAwait(false), Is.False, "sword with mount");

        await EquipAsync(player, InventoryConstants.RightHandSlot, group: 3, number: 0).ConfigureAwait(false); // Spear
        Assert.That(await AttackActionValidator.CanAttackAsync(player, impale).ConfigureAwait(false), Is.True, "spear with mount");
    }

    /// <summary>
    /// Power Slash (56) and its master variant (482) require a melee weapon and refuse when riding a mount.
    /// </summary>
    /// <param name="skillNumber">The base or strengthened Power Slash skill number.</param>
    [TestCase((short)56)]
    [TestCase((short)482)]
    public async ValueTask PowerSlashRequiresMeleeAndNoMountAsync(short skillNumber)
    {
        var player = await CreateReadyPlayerAsync().ConfigureAwait(false);
        var skill = new Skill { Number = skillNumber, DamageType = DamageType.Physical };

        Assert.That(await AttackActionValidator.CanAttackAsync(player, skill).ConfigureAwait(false), Is.False, "unarmed");

        await EquipAsync(player, InventoryConstants.RightHandSlot, group: 0, number: 0).ConfigureAwait(false); // Sword
        Assert.That(await AttackActionValidator.CanAttackAsync(player, skill).ConfigureAwait(false), Is.True, "sword without mount");

        await EquipAsync(player, InventoryConstants.PetSlot, ItemConstants.Fenrir).ConfigureAwait(false);
        Assert.That(await AttackActionValidator.CanAttackAsync(player, skill).ConfigureAwait(false), Is.False, "refused while riding Fenrir");
    }

    /// <summary>
    /// Death Stab (43 / 336), Spiral Slash (57), Fire Slash (55 / 490 / 493), Rageful Blow (42),
    /// Strike of Destruction (232) and Flame Strike (236) enforce their desktop weapon rules.
    /// </summary>
    [Test]
    public async ValueTask KnightAndGladiatorWeaponSkillsEnforceHandRulesAsync()
    {
        var player = await CreateReadyPlayerAsync().ConfigureAwait(false);

        await EquipAsync(player, InventoryConstants.RightHandSlot, group: 5, number: 0).ConfigureAwait(false); // Staff
        Assert.That(await AttackActionValidator.CanAttackAsync(player, new Skill { Number = 43 }).ConfigureAwait(false), Is.False, "Death Stab with staff");
        Assert.That(await AttackActionValidator.CanAttackAsync(player, new Skill { Number = 42 }).ConfigureAwait(false), Is.False, "Rageful Blow with staff");
        Assert.That(await AttackActionValidator.CanAttackAsync(player, new Skill { Number = 55 }).ConfigureAwait(false), Is.False, "Fire Slash with staff");

        await EquipAsync(player, InventoryConstants.RightHandSlot, group: 3, number: 0).ConfigureAwait(false); // Spear
        Assert.That(await AttackActionValidator.CanAttackAsync(player, new Skill { Number = 57 }).ConfigureAwait(false), Is.False, "Spiral Slash needs sword or glove");
        Assert.That(await AttackActionValidator.CanAttackAsync(player, new Skill { Number = 43 }).ConfigureAwait(false), Is.True, "Death Stab allows spear");

        await EquipAsync(player, InventoryConstants.RightHandSlot, group: 0, number: 0).ConfigureAwait(false); // Sword
        Assert.That(await AttackActionValidator.CanAttackAsync(player, new Skill { Number = 57 }).ConfigureAwait(false), Is.True, "Spiral Slash with sword");
        Assert.That(await AttackActionValidator.CanAttackAsync(player, new Skill { Number = 232 }).ConfigureAwait(false), Is.True, "Strike of Destruction with sword");
        Assert.That(await AttackActionValidator.CanAttackAsync(player, new Skill { Number = 236 }).ConfigureAwait(false), Is.True, "Flame Strike with sword");
    }

    /// <summary>
    /// Earthshake (62 / 512 / 516) requires a living Dark Horse, and Plasma Storm (76) requires a living Fenrir.
    /// </summary>
    [Test]
    public async ValueTask MountSkillsRequireTheirMatchingLivingMountAsync()
    {
        var player = await CreateReadyPlayerAsync().ConfigureAwait(false);
        var earthshake = new Skill { Number = 62, DamageType = DamageType.Physical };
        var plasmaStorm = new Skill { Number = 76, DamageType = DamageType.Fenrir };

        Assert.That(await AttackActionValidator.CanAttackAsync(player, earthshake).ConfigureAwait(false), Is.False);
        Assert.That(await AttackActionValidator.CanAttackAsync(player, plasmaStorm).ConfigureAwait(false), Is.False);

        await EquipAsync(player, InventoryConstants.PetSlot, ItemConstants.DarkHorse, durability: 0).ConfigureAwait(false);
        Assert.That(await AttackActionValidator.CanAttackAsync(player, earthshake).ConfigureAwait(false), Is.False, "dead Dark Horse");

        await EquipAsync(player, InventoryConstants.PetSlot, ItemConstants.DarkHorse, durability: 100).ConfigureAwait(false);
        Assert.That(await AttackActionValidator.CanAttackAsync(player, earthshake).ConfigureAwait(false), Is.True);
        Assert.That(await AttackActionValidator.CanAttackAsync(player, plasmaStorm).ConfigureAwait(false), Is.False, "Dark Horse cannot cast Plasma Storm");

        await EquipAsync(player, InventoryConstants.PetSlot, ItemConstants.Fenrir, durability: 100).ConfigureAwait(false);
        Assert.That(await AttackActionValidator.CanAttackAsync(player, plasmaStorm).ConfigureAwait(false), Is.True);
    }

    /// <summary>
    /// Fairy Elf bow skills require a bow or crossbow (not ammunition alone or empty hands).
    /// </summary>
    /// <param name="skillNumber">The bow skill number.</param>
    [TestCase((short)24)]
    [TestCase((short)51)]
    [TestCase((short)52)]
    [TestCase((short)235)]
    [TestCase((short)418)]
    public async ValueTask ElfBowSkillsRequireBowOrCrossbowAsync(short skillNumber)
    {
        var player = await CreateReadyPlayerAsync().ConfigureAwait(false);
        var skill = new Skill { Number = skillNumber, DamageType = DamageType.Physical };

        Assert.That(await AttackActionValidator.CanAttackAsync(player, skill).ConfigureAwait(false), Is.False, "unarmed");

        await EquipAsync(player, InventoryConstants.LeftHandSlot, group: 4, number: 0).ConfigureAwait(false); // Bow
        Assert.That(await AttackActionValidator.CanAttackAsync(player, skill).ConfigureAwait(false), Is.True, "bow equipped");
    }

    /// <summary>
    /// Rage Fighter skills refuse when riding a horn mount (260, 261, 262, 265, 269) or without swordform gloves (262, 264, 265, 558).
    /// </summary>
    [Test]
    public async ValueTask RageFighterSkillsEnforceGlovesAndNoHornMountAsync()
    {
        var player = await CreateReadyPlayerAsync().ConfigureAwait(false);

        Assert.That(await AttackActionValidator.CanAttackAsync(player, new Skill { Number = 264 }).ConfigureAwait(false), Is.False, "Dragon Roar without gloves");
        Assert.That(await AttackActionValidator.CanAttackAsync(player, new Skill { Number = 558 }).ConfigureAwait(false), Is.False, "Chain Drive Strengthener without gloves");

        await EquipAsync(player, InventoryConstants.RightHandSlot, group: 0, number: 38).ConfigureAwait(false); // S9 Frick Large Glove
        Assert.That(await AttackActionValidator.CanAttackAsync(player, new Skill { Number = 264 }).ConfigureAwait(false), Is.True, "Dragon Roar with S9 glove");
        Assert.That(await AttackActionValidator.CanAttackAsync(player, new Skill { Number = 558 }).ConfigureAwait(false), Is.True, "Chain Drive Strengthener with S9 glove");

        await EquipAsync(player, InventoryConstants.PetSlot, ItemConstants.Fenrir).ConfigureAwait(false);
        Assert.That(await AttackActionValidator.CanAttackAsync(player, new Skill { Number = 260 }).ConfigureAwait(false), Is.False, "Killing Blow on Fenrir");
        Assert.That(await AttackActionValidator.CanAttackAsync(player, new Skill { Number = 269 }).ConfigureAwait(false), Is.False, "Charge on Fenrir");
        Assert.That(await AttackActionValidator.CanAttackAsync(player, new Skill { Number = 264 }).ConfigureAwait(false), Is.True, "Dragon Roar allowed on Fenrir");
    }

    private static async ValueTask<Player> CreateReadyPlayerAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Position = new Pathfinding.Point(100, 100);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
        return player;
    }

    private static async ValueTask EquipAsync(Player player, byte slot, ItemIdentifier identifier, double durability = 100)
        => await EquipAsync(player, slot, identifier.Group, identifier.Number ?? 0, durability).ConfigureAwait(false);

    private static async ValueTask EquipAsync(Player player, byte slot, byte group, short number, double durability = 100)
    {
        if (player.Inventory!.GetItem(slot) is { } existing)
        {
            await player.Inventory.RemoveItemAsync(existing).ConfigureAwait(false);
        }

        var item = new BasicModel.Item
        {
            Definition = new BasicModel.ItemDefinition
            {
                Group = group,
                Number = number,
                Width = 1,
                Height = 1,
                ItemSlot = new BasicModel.ItemSlotType { ItemSlots = { slot } },
                Durability = 100,
            },
            Durability = durability,
        };
        await player.Inventory.AddItemAsync(slot, item).ConfigureAwait(false);
    }
}
