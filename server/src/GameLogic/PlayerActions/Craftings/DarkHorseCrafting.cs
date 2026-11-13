// <copyright file="DarkHorseCrafting.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Craftings;

using HNMO.DataModel.Configuration.ItemCrafting;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.PlayerActions.Items;

/// <summary>
/// Crafting for Dark Horse.
/// </summary>
public class DarkHorseCrafting : SimpleItemCraftingHandler
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DarkHorseCrafting"/> class.
    /// </summary>
    /// <param name="settings">The settings.</param>
    public DarkHorseCrafting(SimpleCraftingSettings settings)
        : base(settings)
    {
    }

    /// <inheritdoc/>
    protected override void AddRandomItemOption(Item resultItem, Player player, byte successRate)
    {
        if (resultItem.Definition!.PossibleItemOptions.FirstOrDefault(o =>
                    o.PossibleOptions.Any(p => p.OptionType == ItemOptionTypes.DarkHorse))
                is { } horseOptions)
        {
            foreach (var option in horseOptions.PossibleOptions)
            {
                var link = player.PersistenceContext.CreateNew<ItemOptionLink>();
                link.ItemOption = option;
                resultItem.ItemOptions.Add(link);
            }
        }
    }
}