// <copyright file="CraftingMaxExcellentOptionsTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel.Configuration.ItemCrafting;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Items;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// The crafting setting <see cref="SimpleCraftingSettings.ResultItemMaxExcOptionCount"/> limits the number of
/// excellent options of a crafted item, e.g. one for the Golden Cherry Blossom mix (100 % chance, at most one).
/// </summary>
[TestFixture]
public class CraftingMaxExcellentOptionsTest
{
    /// <summary>With a 100 % chance and a maximum of one, the item gets exactly one excellent option.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task MaximumOfOneIsKeptAsync()
    {
        var item = await this.CraftAsync(maximum: 1).ConfigureAwait(false);

        Assert.That(item.ItemOptions.Count(o => o.ItemOption?.OptionType == ItemOptionTypes.Excellent), Is.EqualTo(1));
    }

    /// <summary>Without a maximum (0), the option definition's own maximum applies, as before.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NoMaximumUsesTheOptionDefinitionAsync()
    {
        var item = await this.CraftAsync(maximum: 0).ConfigureAwait(false);

        Assert.That(item.ItemOptions.Count(o => o.ItemOption?.OptionType == ItemOptionTypes.Excellent), Is.EqualTo(2));
    }

    private async Task<Item> CraftAsync(byte maximum)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync(GameContextTestHelper.CreateGameContext()).ConfigureAwait(false);
        var settings = new BasicModel.SimpleCraftingSettings
        {
            ResultItemExcellentOptionChance = 100,
            ResultItemMaxExcOptionCount = maximum,
        };

        var excellentOptions = new BasicModel.ItemOptionDefinition { MaximumOptionsPerItem = 2, AddChance = 0.001f };
        for (var number = 1; number <= 6; number++)
        {
            excellentOptions.PossibleOptions.Add(new BasicModel.IncreasableItemOption { Id = Guid.NewGuid(), Number = number, OptionType = ItemOptionTypes.Excellent });
        }

        var definition = new BasicModel.ItemDefinition { Group = 8, Number = 1 };
        definition.PossibleItemOptions.Add(excellentOptions);
        var item = new BasicModel.Item { Definition = definition, Durability = 1 };

        new ExposedCraftingHandler(settings).AddExcellentOptions(item, player);
        return item;
    }

    private sealed class ExposedCraftingHandler : SimpleItemCraftingHandler
    {
        public ExposedCraftingHandler(SimpleCraftingSettings settings)
            : base(settings)
        {
        }

        public void AddExcellentOptions(Item item, Player player) => this.AddRandomExcellentOptions(item, player);
    }
}
