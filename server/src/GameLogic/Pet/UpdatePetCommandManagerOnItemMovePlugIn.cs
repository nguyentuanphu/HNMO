// <copyright file="UpdatePetCommandManagerOnItemMovePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Pet;

using System.Runtime.InteropServices;
using HNMO.GameLogic.PlayerActions.Items;
using HNMO.GameLogic.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Updates the pet command manager on the player object after a pet has been equipped or unequipped.
/// </summary>
[Guid("E25CBAE0-CFD2-4BAF-9178-4FBD90F26E4B")]
[PlugIn]
[Display(Name = nameof(PlugInResources.UpdatePetCommandManagerOnItemMovePlugIn_Name), Description = nameof(PlugInResources.UpdatePetCommandManagerOnItemMovePlugIn_Description), ResourceType = typeof(PlugInResources))]
public class UpdatePetCommandManagerOnItemMovePlugIn : IItemMovedPlugIn
{
    /// <inheritdoc />
    public async ValueTask ItemMovedAsync(Player player, Item item)
    {
        if (player.Inventory is null)
        {
            return;
        }

        var isAttackPet = item.IsTrainablePet() && !item.IsDefensiveItem();
        if (!isAttackPet)
        {
            return;
        }

        var itemAdded = player.Inventory.EquippedItems.Contains(item);
        if (itemAdded && player.PetCommandManager is { } petCommandManager)
        {
            await petCommandManager.SetBehaviourAsync(PetBehaviour.Idle, null).ConfigureAwait(false);
        }
        else
        {
            player.RemovePetCommandManager();
        }
    }
}