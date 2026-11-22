// <copyright file="ManaRaysWeaponRuleTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Skills;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// Verifies that Mana Rays (skill 73, desktop <c>AT_SKILL_DEATH_CANNON</c>) enforces the same
/// right-hand staff/book weapon rule on the server as in the desktop and web clients.
/// </summary>
[TestFixture]
public class ManaRaysWeaponRuleTests
{
    private const short ManaRaysSkillId = 73;

    /// <summary>
    /// Mana Rays is refused when unarmed or holding a sword, and allowed when holding a staff or book in the right hand.
    /// </summary>
    [Test]
    public async ValueTask ManaRaysRequiresStaffOrBookInRightHandAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Position = new Pathfinding.Point(100, 100);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
        var manaRays = new Skill { Number = ManaRaysSkillId, DamageType = DamageType.Wizardry };

        Assert.That(await AttackActionValidator.CanAttackAsync(player, manaRays).ConfigureAwait(false), Is.False, "unarmed");

        await EquipRightHandAsync(player, group: 0, number: 23).ConfigureAwait(false); // Explosion Blade (sword)
        Assert.That(await AttackActionValidator.CanAttackAsync(player, manaRays).ConfigureAwait(false), Is.False, "sword");

        await EquipRightHandAsync(player, group: 5, number: 0).ConfigureAwait(false); // Staff
        Assert.That(await AttackActionValidator.CanAttackAsync(player, manaRays).ConfigureAwait(false), Is.True, "staff");
    }

    private static async ValueTask EquipRightHandAsync(Player player, byte group, short number)
    {
        if (player.Inventory!.GetItem(InventoryConstants.RightHandSlot) is { } existing)
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
                ItemSlot = new BasicModel.ItemSlotType { ItemSlots = { InventoryConstants.RightHandSlot } },
                Durability = 100,
            },
            Durability = 100,
        };
        await player.Inventory.AddItemAsync(InventoryConstants.RightHandSlot, item).ConfigureAwait(false);
    }
}
