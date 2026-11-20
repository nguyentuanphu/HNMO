// <copyright file="JewelOfLifeOption28Test.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.ItemConsumeActions;
using HNMO.GameLogic.PlugIns.GameplayRules;
using HNMO.Persistence.InMemory;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// The Jewel of Life raises the additional option of the Season 6 items up to +28 (option level 7), like the
/// Season 6/9 server suites.
/// </summary>
[TestFixture]
public class JewelOfLifeOption28Test
{
    private GameConfiguration _gameConfiguration = null!;

    /// <summary>Creates the Season 6 data.</summary>
    /// <returns>The task.</returns>
    [OneTimeSetUp]
    public async Task SetUpAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        await new Persistence.Initialization.VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory())
            .CreateInitialDataAsync(1, false).ConfigureAwait(false);
        this._gameConfiguration = (await contextProvider.CreateNewConfigurationContext().GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
    }

    /// <summary>
    /// A Jewel of Life raises a weapon's, an armour piece's, a shield's and a wing's +16 option to +20, and on up to
    /// +28, where it stops. With the rate tuning on, its default cap is +28, too.
    /// </summary>
    /// <param name="group">The item group.</param>
    /// <param name="number">The item number.</param>
    /// <param name="rateTuningEnabled">Whether the rate tuning (with its default cap) is on.</param>
    /// <returns>The task.</returns>
    [TestCase(0, 0, false)] // Kris
    [TestCase(8, 0, false)] // Bronze Armor
    [TestCase(6, 0, false)] // Small Shield
    [TestCase(12, 2, false)] // Wings of Satan
    [TestCase(0, 0, true)]
    public async Task JewelOfLifeRaisesTheOptionFromLevelFourToSevenAsync(byte group, short number, bool rateTuningEnabled)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        if (rateTuningEnabled)
        {
            player.GameContext.FeaturePlugIns.AddPlugIn(new RateTuningPlugIn { Configuration = new RateTuningConfiguration { Enabled = true } }, true);
        }

        var definition = this._gameConfiguration.Items.First(i => i.Group == group && i.Number == number);
        var item = player.PersistenceContext.CreateNew<Item>();
        item.Definition = definition;
        item.Durability = definition.Durability;
        var optionLink = player.PersistenceContext.CreateNew<ItemOptionLink>();
        optionLink.ItemOption = definition.PossibleItemOptions.SelectMany(o => o.PossibleOptions).First(o => o.OptionType == ItemOptionTypes.Option);
        optionLink.Level = 4;
        item.ItemOptions.Add(optionLink);
        await player.Inventory!.AddItemAsync(20, item).ConfigureAwait(false);

        var handler = new LifeJewelConsumeHandlerPlugIn
        {
            Configuration = new ItemUpgradeConfiguration { SuccessChance = 1 },
        };

        for (var expectedLevel = 5; expectedLevel <= 7; expectedLevel++)
        {
            var jewel = CreateJewel();
            await player.Inventory.AddItemAsync(12, jewel).ConfigureAwait(false);
            var consumed = await handler.ConsumeItemAsync(player, jewel, item, FruitUsage.Undefined).ConfigureAwait(false);
            Assert.That(consumed, Is.True, () => $"to level {expectedLevel}");
            Assert.That(optionLink.Level, Is.EqualTo(expectedLevel));
        }

        var lastJewel = CreateJewel();
        await player.Inventory.AddItemAsync(12, lastJewel).ConfigureAwait(false);
        var consumedAtMaximum = await handler.ConsumeItemAsync(player, lastJewel, item, FruitUsage.Undefined).ConfigureAwait(false);
        Assert.That(consumedAtMaximum, Is.False, "+28 is the maximum");
        Assert.That(optionLink.Level, Is.EqualTo(7));
    }

    private static Item CreateJewel()
    {
        return new Item
        {
            Definition = new ItemDefinition { Width = 1, Height = 1 },
            Durability = 1,
        };
    }
}
