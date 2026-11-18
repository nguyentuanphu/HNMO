// <copyright file="GiftBoxPacking.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GiftBox;

using HNMO.DataModel;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Administration;
using HNMO.GameLogic.PlugIns.GiftCodes;
using static HNMO.DataModel.InventoryConstants;

/// <summary>Uses the existing inventory grid on detached items before any mail delivery mutation.</summary>
public static class GiftBoxPacking
{
    /// <summary>Plans exact item options and slots, including separate inventory extensions.</summary>
    /// <param name="configuration">The authoritative item definitions.</param>
    /// <param name="existingItems">The real bag and equipped items; never mutated by this method.</param>
    /// <param name="extensions">The character's available inventory extensions.</param>
    /// <param name="reward">The promised reward; coins and Zen are checked by the collection adapter.</param>
    /// <returns>The refusal or detached items with final slots. A refusal returns no deliverable items.</returns>
    public static async ValueTask<(GiftBoxPackingStatus Status, IReadOnlyList<TemporaryItem> Items)> PlanAsync(GameConfiguration configuration, IEnumerable<Item> existingItems, int extensions, GiftCodeReward reward)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(existingItems);
        ArgumentNullException.ThrowIfNull(reward);
        if (extensions < 0 || extensions > MaximumNumberOfExtensions)
        {
            return (GiftBoxPackingStatus.InvalidInventory, []);
        }

        var real = existingItems.Where(item => item.ItemSlot < FirstStoreItemSlotIndex).ToArray();
        if (real.Any(item => item.Definition is null || item.ItemSlot >= GetInventorySize(extensions)))
        {
            return (GiftBoxPackingStatus.InvalidInventory, []);
        }

        var copies = real.Select(item => new TemporaryItem { Definition = item.Definition, ItemSlot = item.ItemSlot }).ToArray();
        var originalSlots = copies.Select(item => item.ItemSlot).ToArray();
        ScratchInventory inventory;
        try
        {
            inventory = new(new ScratchStorage(copies), extensions);
        }
        catch (ArgumentException)
        {
            return (GiftBoxPackingStatus.InvalidInventory, []);
        }

        // Storage can repair malformed layouts while loading. Such repairs must never become a side effect
        // of collecting a gift, even on the detached plan: refuse until the real layout is investigated.
        if (copies.Where((item, index) => item.ItemSlot != originalSlots[index]).Any())
        {
            return (GiftBoxPackingStatus.InvalidInventory, []);
        }

        if (reward.Items is null || reward.Items.Count > 50
            || reward.Items.Any(item => item is null || item.Count is < 1 or > 50)
            || reward.Items.Sum(item => item.Count) > 50)
        {
            return (GiftBoxPackingStatus.InvalidReward, []);
        }

        var planned = new List<TemporaryItem>();
        foreach (var entry in reward.Items)
        {
            var definition = configuration.Items.FirstOrDefault(item => item.Group == entry.Group && item.Number == entry.Number);
            var specification = new ItemSpecification((byte)entry.Group, entry.Number, entry.Level, entry.Skill, entry.Luck, entry.OptionLevel, entry.ExcellentOptions);
            if (definition is null || !GameMasterItemFactory.IsValid(definition, specification))
            {
                return (GiftBoxPackingStatus.InvalidReward, []);
            }

            for (var index = 0; index < entry.Count; index++)
            {
                var item = GameMasterItemFactory.CreateItem(definition, specification);
                if (item.IsStackable())
                {
                    item.Durability = Math.Clamp(entry.Durability == 0 ? definition.Durability : entry.Durability, 1, Math.Max(1, (int)definition.Durability));
                }
                else
                {
                    var maximum = item.GetMaximumDurabilityOfOnePiece();
                    item.Durability = entry.Durability == 0 ? maximum : Math.Min(entry.Durability, maximum);
                }

                if (inventory.CheckInvSpace(item) is not { } slot || !await inventory.AddItemAsync(slot, item).ConfigureAwait(false))
                {
                    return (GiftBoxPackingStatus.BagFull, []);
                }

                planned.Add(item);
            }
        }

        return (GiftBoxPackingStatus.Ready, planned);
    }

    private sealed class ScratchStorage : ItemStorage
    {
        public ScratchStorage(IEnumerable<Item> items)
        {
            this.Items = items.ToList();
        }
    }

    private sealed class ScratchInventory : Storage
    {
        public ScratchInventory(ItemStorage storage, int extensions)
            : base(GetInventorySize(0), EquippableSlotsCount, 0, storage)
        {
            this.Extensions = Enumerable.Range(0, extensions)
                .Select(index => new Storage(RowsOfOneExtension * RowSize, 0, FirstExtensionItemSlotIndex + (index * RowsOfOneExtension * RowSize), storage)).ToArray();
        }
    }
}
