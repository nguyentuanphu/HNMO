// <copyright file="SecondWingsCrafting.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Craftings;

using HNMO.DataModel.Configuration.ItemCrafting;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.PlayerActions.Items;

/// <summary>
/// Crafting for Second Wings (including first capes).
/// </summary>
public class SecondWingsCrafting : SimpleItemCraftingHandler
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SecondWingsCrafting"/> class.
    /// </summary>
    /// <param name="settings">The settings.</param>
    public SecondWingsCrafting(SimpleCraftingSettings settings)
        : base(settings)
    {
    }

    /// <inheritdoc/>
    protected override void AddRandomItemOption(Item resultItem, Player player, byte successRate)
    {
        if (resultItem.Definition!.PossibleItemOptions.Where(o => o.PossibleOptions.Any(p => p.OptionType == ItemOptionTypes.Option))
                is { } options && options.Any())
        {
            (int chance, int level) = Rand.NextInt(0, 3) switch
            {
                0 => (20, 1),
                1 => (10, 2),
                _ => (4, 3),
            }; // From 300 created wings about 20+10+4=34 (~11%) will have item option

            if (Rand.NextRandomBool(chance))
            {
                var link = player.PersistenceContext.CreateNew<ItemOptionLink>();
                link.Level = level;
                if (options.Count() > 1)
                {
                    link.ItemOption = options.ElementAt(Rand.NextInt(0, 2)).PossibleOptions.First();
                }
                else
                {
                    link.ItemOption = options.ElementAt(0).PossibleOptions.First(); // Cape of Lord
                }

                resultItem.ItemOptions.Add(link);
            }
        }
    }
}