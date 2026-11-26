// <copyright file="PentagramActions.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.SeasonNineSystems;

using HNMO.DataModel.Entities;
using HNMO.GameLogic.Views.Inventory;

/// <summary>The outcome of a pentagram action.</summary>
public enum PentagramActionResult
{
    /// <summary>Done.</summary>
    Ok,

    /// <summary>The Season 9 pentagram system is off in this realm.</summary>
    Disabled,

    /// <summary>The item isn't a pentagram (or an errtel), or the slot is wrong.</summary>
    InvalidItem,

    /// <summary>A pentagram is already worn, or the errtel slot is taken.</summary>
    Occupied,

    /// <summary>Nothing is worn, or the errtel slot is empty.</summary>
    Empty,

    /// <summary>The errtel's element differs from the pentagram's.</summary>
    ElementMismatch,

    /// <summary>The bag has no room.</summary>
    BagFull,

    /// <summary>The change couldn't be saved; nothing changed.</summary>
    SaveFailed,
}

/// <summary>
/// Wearing and taking off the Season 9 pentagram (slot <see cref="InventoryConstants.PentagramSlot"/> of the
/// inventory's item storage, after the personal store) and putting errtels into its slots and back into the bag. Each
/// action is saved at once and undone in memory when the save fails, so the running game and the database agree.
/// </summary>
public static class PentagramActions
{
    /// <summary>Wears the pentagram in a bag slot.</summary>
    /// <param name="player">The player.</param>
    /// <param name="bagSlot">The bag slot of the pentagram.</param>
    /// <returns>The outcome.</returns>
    public static ValueTask<PentagramActionResult> EquipAsync(Player player, byte bagSlot)
        => player.RunPersistenceExclusiveAsync(async () =>
        {
            if (!IsOn(player))
            {
                return PentagramActionResult.Disabled;
            }

            if (player.Inventory?.GetItem(bagSlot) is not { } item || bagSlot < InventoryConstants.EquippableSlotsCount || !Pentagram.IsPentagram(item.Definition))
            {
                return PentagramActionResult.InvalidItem;
            }

            if (Pentagram.Worn(player) is not null)
            {
                return PentagramActionResult.Occupied;
            }

            await player.Inventory.RemoveItemAsync(item).ConfigureAwait(false);
            item.ItemSlot = InventoryConstants.PentagramSlot;
            player.SelectedCharacter!.Inventory!.Items.Add(item);
            if (!await SaveAsync(player).ConfigureAwait(false))
            {
                player.SelectedCharacter.Inventory.Items.Remove(item);
                await player.Inventory.AddItemAsync(bagSlot, item).ConfigureAwait(false);
                return PentagramActionResult.SaveFailed;
            }

            await player.InvokeViewPlugInAsync<IItemRemovedPlugIn>(p => p.RemoveItemAsync(bagSlot)).ConfigureAwait(false);
            return PentagramActionResult.Ok;
        });

    /// <summary>Takes off the worn pentagram into the first free bag slot.</summary>
    /// <param name="player">The player.</param>
    /// <returns>The outcome.</returns>
    public static ValueTask<PentagramActionResult> UnequipAsync(Player player)
        => player.RunPersistenceExclusiveAsync(async () =>
        {
            if (Pentagram.Worn(player) is not { } pentagram)
            {
                return PentagramActionResult.Empty;
            }

            if (player.Inventory?.CheckInvSpace(pentagram) is not { } freeSlot)
            {
                return PentagramActionResult.BagFull;
            }

            player.SelectedCharacter!.Inventory!.Items.Remove(pentagram);
            if (!await player.Inventory.AddItemAsync(freeSlot, pentagram).ConfigureAwait(false))
            {
                pentagram.ItemSlot = InventoryConstants.PentagramSlot;
                player.SelectedCharacter.Inventory.Items.Add(pentagram);
                return PentagramActionResult.BagFull;
            }

            if (!await SaveAsync(player).ConfigureAwait(false))
            {
                await player.Inventory.RemoveItemAsync(pentagram).ConfigureAwait(false);
                pentagram.ItemSlot = InventoryConstants.PentagramSlot;
                player.SelectedCharacter.Inventory.Items.Add(pentagram);
                return PentagramActionResult.SaveFailed;
            }

            await player.InvokeViewPlugInAsync<IItemAppearPlugIn>(p => p.ItemAppearAsync(pentagram)).ConfigureAwait(false);
            return PentagramActionResult.Ok;
        });

    /// <summary>Puts the errtel of a bag slot into an errtel slot of the worn pentagram.</summary>
    /// <param name="player">The player.</param>
    /// <param name="errtelBagSlot">The errtel's bag slot.</param>
    /// <param name="slotIndex">The pentagram's errtel slot (0..4).</param>
    /// <returns>The outcome.</returns>
    public static ValueTask<PentagramActionResult> AttachErrtelAsync(Player player, byte errtelBagSlot, int slotIndex)
        => player.RunPersistenceExclusiveAsync(async () =>
        {
            if (!IsOn(player))
            {
                return PentagramActionResult.Disabled;
            }

            if (Pentagram.Worn(player) is not { } pentagram)
            {
                return PentagramActionResult.Empty;
            }

            if (player.Inventory?.GetItem(errtelBagSlot) is not { } errtel || errtelBagSlot < InventoryConstants.EquippableSlotsCount
                || !Pentagram.IsErrtel(errtel.Definition) || slotIndex < 0 || slotIndex >= Pentagram.UsableSlots(pentagram.Definition!))
            {
                return PentagramActionResult.InvalidItem;
            }

            var slots = Pentagram.ReadSlots(pentagram);
            if (slots[slotIndex] is not null)
            {
                return PentagramActionResult.Occupied;
            }

            var info = Pentagram.ReadErrtel(errtel);
            if (info.Element != (PentagramElement)pentagram.PentagramElement)
            {
                return PentagramActionResult.ElementMismatch;
            }

            var before = pentagram.PentagramErrtels;
            slots[slotIndex] = info;
            Pentagram.WriteSlots(pentagram, slots);
            await player.Inventory.RemoveItemAsync(errtel).ConfigureAwait(false);
            await player.PersistenceContext.DeleteAsync(errtel).ConfigureAwait(false);
            if (!await SaveAsync(player).ConfigureAwait(false))
            {
                // The deleted errtel stays deleted; a copy of it goes back where it was, so the next save writes the
                // bag as it was before (the same errtel, the pentagram without it).
                pentagram.PentagramErrtels = before;
                var copy = player.PersistenceContext.CreateNew<Item>();
                copy.AssignValues(errtel);
                await player.Inventory.AddItemAsync(errtelBagSlot, copy).ConfigureAwait(false);
                return PentagramActionResult.SaveFailed;
            }

            await player.InvokeViewPlugInAsync<IItemRemovedPlugIn>(p => p.RemoveItemAsync(errtelBagSlot)).ConfigureAwait(false);
            return PentagramActionResult.Ok;
        });

    /// <summary>Takes the errtel of an errtel slot of the worn pentagram back into the bag (all or nothing).</summary>
    /// <param name="player">The player.</param>
    /// <param name="slotIndex">The pentagram's errtel slot (0..4).</param>
    /// <returns>The outcome.</returns>
    public static ValueTask<PentagramActionResult> RemoveErrtelAsync(Player player, int slotIndex)
        => player.RunPersistenceExclusiveAsync(async () =>
        {
            if (!IsOn(player))
            {
                return PentagramActionResult.Disabled;
            }

            if (Pentagram.Worn(player) is not { } pentagram || slotIndex < 0 || slotIndex >= Pentagram.SlotCount)
            {
                return PentagramActionResult.Empty;
            }

            var slots = Pentagram.ReadSlots(pentagram);
            if (slots[slotIndex] is not { } info)
            {
                return PentagramActionResult.Empty;
            }

            var definition = player.GameContext.Configuration.Items.FirstOrDefault(d => d.Group == 12 && d.Number == info.Number);
            if (definition is null)
            {
                return PentagramActionResult.InvalidItem;
            }

            var errtel = player.PersistenceContext.CreateNew<Item>();
            errtel.Definition = definition;
            errtel.Durability = 1;
            Pentagram.WriteErrtel(errtel, info);
            if (player.Inventory?.CheckInvSpace(errtel) is not { } freeSlot || !await player.Inventory.AddItemAsync(freeSlot, errtel).ConfigureAwait(false))
            {
                await player.PersistenceContext.DeleteAsync(errtel).ConfigureAwait(false);
                return PentagramActionResult.BagFull;
            }

            var before = pentagram.PentagramErrtels;
            slots[slotIndex] = null;
            Pentagram.WriteSlots(pentagram, slots);
            if (!await SaveAsync(player).ConfigureAwait(false))
            {
                pentagram.PentagramErrtels = before;
                await player.Inventory.RemoveItemAsync(errtel).ConfigureAwait(false);
                await player.PersistenceContext.DeleteAsync(errtel).ConfigureAwait(false);
                return PentagramActionResult.SaveFailed;
            }

            await player.InvokeViewPlugInAsync<IItemAppearPlugIn>(p => p.ItemAppearAsync(errtel)).ConfigureAwait(false);
            return PentagramActionResult.Ok;
        });

    private static bool IsOn(Player player)
        => SeasonNineSystemsPlugIn.Get(player.GameContext) is { } plugIn && plugIn.IsActive() && plugIn.Configuration is { PentagramEnabled: true };

    private static async ValueTask<bool> SaveAsync(Player player)
    {
        try
        {
            return await player.SaveProgressAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            player.Logger.LogWarning(ex, "Pentagram: the change of {Player} couldn't be saved.", player.Name);
            return false;
        }
    }
}
