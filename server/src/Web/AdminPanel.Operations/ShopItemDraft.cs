// <copyright file="ShopItemDraft.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Operations;

using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.Persistence;

/// <summary>An isolated item with editable option links and stable identity for undo and save comparisons.</summary>
public sealed class ShopItemDraft : Item
{
    /// <summary>Initializes a new instance of the <see cref="ShopItemDraft"/> class.</summary>
    public ShopItemDraft()
    {
        this.ItemOptions = new List<ItemOptionLink>();
        this.ItemSetGroups = new List<ItemOfItemSet>();
    }

    /// <summary>Gets or sets the editor identity; it is not a new persistence field.</summary>
    public Guid Key { get; set; } = Guid.NewGuid();

    /// <summary>Copies an item without sharing mutable option links.</summary>
    /// <param name="item">The source.</param>
    /// <param name="newIdentity">Whether this is a new shop entry.</param>
    /// <returns>The detached copy.</returns>
    public static ShopItemDraft Copy(Item item, bool newIdentity = false)
    {
        var copy = new ShopItemDraft { Key = newIdentity ? Guid.NewGuid() : item is ShopItemDraft draft ? draft.Key : item.GetId() };
        CopyValues(item, copy, () => new ItemOptionLink());
        return copy;
    }

    /// <summary>Transfers values, including cleared options, without duplicating configuration definitions.</summary>
    /// <param name="source">The source.</param>
    /// <param name="target">The destination.</param>
    /// <param name="optionFactory">Creates detached or tracked option links.</param>
    public static void CopyValues(Item source, Item target, Func<ItemOptionLink> optionFactory)
    {
        target.Definition = source.Definition;
        target.ItemSlot = source.ItemSlot;
        target.Level = source.Level;
        target.Durability = source.Durability;
        target.HasSkill = source.HasSkill;
        target.SocketCount = source.SocketCount;
        target.PetExperience = source.PetExperience;
        target.StorePrice = source.StorePrice;
        target.StorePriceItemDefinition = source.StorePriceItemDefinition;
        target.StorePriceCoinType = source.StorePriceCoinType;
        target.ItemOptions.Clear();
        foreach (var link in source.ItemOptions)
        {
            var copy = optionFactory();
            copy.AssignValues(link);
            target.ItemOptions.Add(copy);
        }

        target.ItemSetGroups.Clear();
        foreach (var set in source.ItemSetGroups)
        {
            target.ItemSetGroups.Add(set);
        }
    }

    /// <summary>Gets a culture-independent signature for conflict detection and summaries.</summary>
    /// <param name="includeSlot">Whether movement is included.</param>
    /// <returns>The content signature.</returns>
    public string Signature(bool includeSlot = true) => FormattableString.Invariant(
        $"{(includeSlot ? this.ItemSlot : 0)}|{this.Definition?.GetId()}|{this.Definition?.Group}:{this.Definition?.Number}|{this.Level}|{this.Durability:R}|{this.HasSkill}|{this.SocketCount}|{this.PetExperience}|{this.StorePrice}|{this.StorePriceItemDefinition?.GetId()}|{this.StorePriceCoinType}|{string.Join(';', this.ItemOptions.Select(o => FormattableString.Invariant($"{o.ItemOption?.GetId()}:{o.Level}:{o.Index}")).Order())}|{string.Join(';', this.ItemSetGroups.Select(s => s.GetId()).Order())}");
}
