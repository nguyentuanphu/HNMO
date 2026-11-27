// <copyright file="ChaosCardDataTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlayerActions.Craftings;
using HNMO.Persistence.InMemory;
using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.Initialization.VersionSeasonSix;

/// <summary>
/// Verifies the four Chaos Card items, <c>#375</c> combination recipe, item rules, Cash Shop links,
/// and <see cref="AddChaosCardMixesUpdatePlugIn"/> idempotency.
/// </summary>
[TestFixture]
public class ChaosCardDataTests
{
    /// <summary>
    /// Fresh Season 6 initialization includes all four Chaos Cards, item rules, #375 recipe, and Cash Shop packages.
    /// </summary>
    [Test]
    public async Task SeasonSixInitializationCreatesCardsAndMixAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        var dataInit = new DataInitialization(provider, NullLoggerFactory.Instance);
        await dataInit.CreateInitialDataAsync(1, true).ConfigureAwait(false);

        using var context = provider.CreateNewContext();
        var config = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();

        AssertChaosCardConfig(config);
    }

    /// <summary>
    /// Applying <see cref="AddChaosCardMixesUpdatePlugIn"/> twice onto an older configuration adds the cards
    /// and recipe once and links the Cash Shop Chaos Card packages.
    /// </summary>
    [Test]
    public async Task UpdatePlugInAddsMissingCardsAndMixIdempotentlyAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        var dataInit = new DataInitialization(provider, NullLoggerFactory.Instance);
        await dataInit.CreateInitialDataAsync(1, true).ConfigureAwait(false);

        using var context = provider.CreateNewContext();
        var config = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();

        var master = config.Monsters.First(m => m.NpcWindow == NpcWindow.ChaosCardCombination);
        master.ItemCraftings.Clear();
        foreach (var number in new short[] { 54, 92, 93, 95 })
        {
            if (config.Items.FirstOrDefault(i => i.Group == 14 && i.Number == number) is { } card)
            {
                config.Items.Remove(card);
            }
        }

        foreach (var pkg in config.CashShopConfiguration!.Packages.Where(p => p.PackageSequence is 81 or 141 or 2115 or 2116 or 2117 or 2118))
        {
            foreach (var prod in pkg.Products)
            {
                prod.ItemDefinition = null;
            }

            pkg.IsForSale = false;
        }

        var plugin = new AddChaosCardMixesUpdatePlugIn();
        await plugin.ApplyUpdateAsync(context, config).ConfigureAwait(false);
        await plugin.ApplyUpdateAsync(context, config).ConfigureAwait(false);

        Assert.That(master.ItemCraftings.Count(c => c.Number == ChaosCardCrafting.MixNumber), Is.EqualTo(1));
        AssertChaosCardConfig(config);
    }

    private static void AssertChaosCardConfig(GameConfiguration config)
    {
        var card54 = config.Items.FirstOrDefault(i => i.Group == 14 && i.Number == 54);
        var card92 = config.Items.FirstOrDefault(i => i.Group == 14 && i.Number == 92);
        var card93 = config.Items.FirstOrDefault(i => i.Group == 14 && i.Number == 93);
        var card95 = config.Items.FirstOrDefault(i => i.Group == 14 && i.Number == 95);

        Assert.Multiple(() =>
        {
            Assert.That(card54, Is.Not.Null);
            Assert.That(card92, Is.Not.Null);
            Assert.That(card93, Is.Not.Null);
            Assert.That(card95, Is.Not.Null);

            Assert.That(card54!.IsTradable, Is.True);
            Assert.That(card54.IsStorable, Is.True);
            Assert.That(card54.IsDroppable, Is.False);
            Assert.That(card92!.IsTradable, Is.False);
            Assert.That(card92.IsStorable, Is.True);
            Assert.That(card93!.IsTradable, Is.False);
            Assert.That(card95!.IsTradable, Is.False);
        });

        var master = config.Monsters.Single(m => m.NpcWindow == NpcWindow.ChaosCardCombination);
        var crafting = master.ItemCraftings.Single(c => c.Number == ChaosCardCrafting.MixNumber);
        Assert.Multiple(() =>
        {
            Assert.That(crafting.ItemCraftingHandlerClassName, Is.EqualTo(typeof(ChaosCardCrafting).FullName));
            Assert.That(crafting.SimpleCraftingSettings, Is.Not.Null);
            Assert.That(crafting.SimpleCraftingSettings!.RequiredItems.Single().PossibleItems, Has.Count.EqualTo(4));
            Assert.That(crafting.SimpleCraftingSettings.ResultItems.Count, Is.GreaterThanOrEqualTo(90));
        });

        foreach (var pkgId in new[] { 81, 141, 2115, 2116, 2117, 2118 })
        {
            var package = config.CashShopConfiguration!.Packages.First(p => p.PackageSequence == pkgId);
            Assert.That(package.IsForSale, Is.True, $"Package {pkgId} should be on sale.");
            Assert.That(package.Products.All(p => p.ItemDefinition is not null), Is.True, $"Package {pkgId} products must have ItemDefinition.");
        }
    }
}
