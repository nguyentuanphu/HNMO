// <copyright file="JewelNpcPricesDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.InMemory;

/// <summary>
/// Tests the NPC prices of the jewels (Season 6/9 server suites: Bless 3 M, Life 16 M, Creation 15 M, Guardian 15 M).
/// </summary>
[TestFixture]
internal class JewelNpcPricesDataTest
{
    private static readonly (short Group, short Number, string Formula)[] Expected =
    [
        (14, 13, "3000000"),
        (14, 16, "16000000"),
        (14, 22, "15000000"),
        (14, 31, "15000000"),
        (12, 30, "(level + 1) * 3000000 * 10"),
        (12, 136, "(level + 1) * 16000000 * 10"),
        (12, 137, "(level + 1) * 15000000 * 10"),
        (12, 138, "(level + 1) * 15000000 * 10"),
        (14, 14, "6000000"),
    ];

    /// <summary>A new season 6 database has the suites' prices; Soul stays 6 M.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseHasTheSuitesPricesAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;

        AssertPrices(gameConfiguration);
    }

    /// <summary>The update changes the classic prices; a price the host changed stays; twice is safe.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateChangesTheClassicPricesAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;
        GetDefinition(gameConfiguration, 14, 13).BasePriceFormula = "9000000";
        GetDefinition(gameConfiguration, 14, 16).BasePriceFormula = "45000000";
        GetDefinition(gameConfiguration, 14, 22).BasePriceFormula = "36000000";
        GetDefinition(gameConfiguration, 14, 31).BasePriceFormula = "60000000";
        GetDefinition(gameConfiguration, 12, 30).BasePriceFormula = "(level + 1) * 9000000 * 10";
        GetDefinition(gameConfiguration, 12, 136).BasePriceFormula = "(level + 1) * 45000000 * 10";
        GetDefinition(gameConfiguration, 12, 137).BasePriceFormula = "(level + 1) * 36000000 * 10";
        GetDefinition(gameConfiguration, 12, 138).BasePriceFormula = "(level + 1) * 60000000 * 10";
        var update = new JewelNpcPricesUpdatePlugIn();

        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        AssertPrices(gameConfiguration);

        GetDefinition(gameConfiguration, 14, 13).BasePriceFormula = "5000000";
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        Assert.That(GetDefinition(gameConfiguration, 14, 13).BasePriceFormula, Is.EqualTo("5000000"), "the host's own price stays");
    }

    private static void AssertPrices(GameConfiguration gameConfiguration)
    {
        Assert.Multiple(() =>
        {
            foreach (var (group, number, formula) in Expected)
            {
                Assert.That(GetDefinition(gameConfiguration, group, number).BasePriceFormula, Is.EqualTo(formula), $"{group},{number}");
            }
        });
    }

    private static ItemPriceDefinition GetDefinition(GameConfiguration gameConfiguration, short group, short number)
        => gameConfiguration.Items.Single(i => i.Group == group && i.Number == number).PriceDefinition
           ?? throw new AssertionException($"No price definition for {group},{number}");

    private static async Task<(IContext Context, GameConfiguration GameConfiguration)> CreateSeason6ConfigurationAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);

        var context = contextProvider.CreateNewContext();
        var gameConfiguration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
        return (context, gameConfiguration);
    }
}
