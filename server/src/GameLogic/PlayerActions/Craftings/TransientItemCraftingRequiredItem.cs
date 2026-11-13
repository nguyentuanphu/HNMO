// <copyright file="TransientItemCraftingRequiredItem.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Craftings;

using HNMO.DataModel.Configuration.ItemCrafting;
using HNMO.DataModel.Configuration.Items;

/// <summary>
/// Transient implementation of <see cref="ItemCraftingRequiredItem"/>.
/// </summary>
/// <seealso cref="HNMO.DataModel.Configuration.ItemCrafting.ItemCraftingRequiredItem" />
internal sealed class TransientItemCraftingRequiredItem : ItemCraftingRequiredItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransientItemCraftingRequiredItem"/> class.
    /// </summary>
    public TransientItemCraftingRequiredItem()
    {
        this.PossibleItems = new List<ItemDefinition>();
        this.RequiredItemOptions = new List<ItemOptionType>();
    }
}