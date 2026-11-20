// <copyright file="HeldItems.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

using HNMO.GameLogic.Views.Inventory;
using PersistenceIdentifiable = HNMO.Persistence.IIdentifiable;

/// <summary>
/// Keeps the real item while the market or escrow holds it: the item's own database row moves out of the bag into an
/// item storage of its own (owned by nobody, like a vault page not in use), and later into the buyer's or owner's
/// bag. Nothing is copied, so every detail stays (ancient sets, sockets, harmony, wing options, pet experience, the
/// item's history), and the item can't exist twice.
/// </summary>
public static class HeldItems
{
    /// <summary>What <see cref="GiveAsync"/> did.</summary>
    public enum GiveResult
    {
        /// <summary>The item is in the bag and saved.</summary>
        Given,

        /// <summary>The bag has no room; nothing changed.</summary>
        NoRoom,

        /// <summary>No item is held under the id (given before).</summary>
        Gone,

        /// <summary>Reading or saving failed; nothing changed.</summary>
        Failed,
    }

    /// <summary>
    /// Moves an item out of the player's bag into a storage of its own. The caller saves the player's progress
    /// (one save for the move and the rest of its change); on a failed save it calls <see cref="ReleaseAsync"/>.
    /// </summary>
    /// <param name="player">The player holding the item.</param>
    /// <param name="item">The item, in the player's bag.</param>
    /// <returns>The storage which now holds the item (its id is kept by the caller), or null if it couldn't be moved.</returns>
    public static async ValueTask<Held?> HoldAsync(Player player, Item item)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(item);
        if (player.Inventory is not { } inventory)
        {
            return null;
        }

        var storage = player.PersistenceContext.CreateNew<ItemStorage>();
        var slot = item.ItemSlot;
        await inventory.RemoveItemAsync(item).ConfigureAwait(false);
        item.ItemSlot = 0;
        storage.Items.Add(item);
        await player.InvokeViewPlugInAsync<IItemRemovedPlugIn>(p => p.RemoveItemAsync(slot)).ConfigureAwait(false);
        return new Held(storage is PersistenceIdentifiable identifiable ? identifiable.Id : Guid.Empty, storage, item, slot);
    }

    /// <summary>
    /// Puts an item moved by <see cref="HoldAsync"/> back where it was, when its change couldn't be saved.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="held">What <see cref="HoldAsync"/> returned.</param>
    /// <returns>The task.</returns>
    public static async ValueTask ReleaseAsync(Player player, Held held)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(held);
        if (player.Inventory is not { } inventory)
        {
            return;
        }

        held.Storage.Items.Remove(held.Item);
        await player.PersistenceContext.DeleteAsync(held.Storage).ConfigureAwait(false);
        if (await inventory.AddItemAsync(held.Slot, held.Item).ConfigureAwait(false))
        {
            await player.InvokeViewPlugInAsync<IItemAppearPlugIn>(p => p.ItemAppearAsync(held.Item)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Moves a held item into the player's bag and saves at once, so the item is either still held or in the bag.
    /// </summary>
    /// <param name="player">The player who gets the item.</param>
    /// <param name="storageId">The id <see cref="HoldAsync"/> returned.</param>
    /// <returns>
    /// <see cref="GiveResult.Given"/>; <see cref="GiveResult.NoRoom"/> when the bag has no room (nothing changed);
    /// <see cref="GiveResult.Gone"/> when no item is held under the id (it was given already);
    /// <see cref="GiveResult.Failed"/> when it couldn't be read or saved (nothing changed).
    /// </returns>
    public static async ValueTask<GiveResult> GiveAsync(Player player, Guid storageId)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Inventory is not { } inventory || storageId == Guid.Empty)
        {
            return GiveResult.Failed;
        }

        ItemStorage? storage;
        try
        {
            storage = await player.PersistenceContext.GetByIdAsync<ItemStorage>(storageId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Held items: the storage {StorageId} couldn't be read.", storageId);
            return GiveResult.Failed;
        }

        if (storage is null || storage.Items.FirstOrDefault() is not { } item)
        {
            return GiveResult.Gone;
        }

        if (inventory.CheckInvSpace(item) is not { } slot)
        {
            return GiveResult.NoRoom;
        }

        storage.Items.Remove(item);
        if (!await inventory.AddItemAsync(slot, item).ConfigureAwait(false))
        {
            storage.Items.Add(item);
            return GiveResult.NoRoom;
        }

        if (!await player.SaveProgressAsync().ConfigureAwait(false))
        {
            // Undo in memory; the database still holds the item in its storage.
            await inventory.RemoveItemAsync(item).ConfigureAwait(false);
            item.ItemSlot = 0;
            storage.Items.Add(item);
            player.Logger.LogError("Held items: the item of storage {StorageId} couldn't be saved into the bag of {Name}; it stays held.", storageId, player.Name);
            return GiveResult.Failed;
        }

        // The empty storage goes with the player's next save; left over, it's an empty row and harmless.
        if (storage.Items.Count == 0)
        {
            await player.PersistenceContext.DeleteAsync(storage).ConfigureAwait(false);
        }

        await player.InvokeViewPlugInAsync<IItemAppearPlugIn>(p => p.ItemAppearAsync(item)).ConfigureAwait(false);
        return GiveResult.Given;
    }

    /// <summary>An item moved out of a bag by <see cref="HoldAsync"/>.</summary>
    /// <param name="Id">The id of the storage holding it (kept by the market or escrow).</param>
    /// <param name="Storage">The storage.</param>
    /// <param name="Item">The item.</param>
    /// <param name="Slot">The bag slot it came from.</param>
    public sealed record Held(Guid Id, ItemStorage Storage, Item Item, byte Slot);
}
