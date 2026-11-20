// <copyright file="ExcellentDropChanceDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.InMemory;

/// <summary>
/// Tests the chance of the common excellent item drop (0.2 % per kill, Season 6/9 server suites).
/// </summary>
[TestFixture]
internal class ExcellentDropChanceDataTest
{
    /// <summary>
    /// A new season 6 database drops excellent items with 0.2 %.
    /// </summary>
    [Test]
    public async Task NewDatabaseHasTheChanceAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;

        Assert.That(GetExcellentGroup(gameConfiguration).Chance, Is.EqualTo(0.002).Within(0.0000001));
    }

    /// <summary>
    /// The update raises 0.01 % to 0.2 %; applying it twice is safe; a chance the host changed is kept.
    /// </summary>
    [Test]
    public async Task UpdateRaisesTheOldChanceOnlyAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;
        var group = GetExcellentGroup(gameConfiguration);
        var update = new ExcellentDropChanceUpdatePlugIn();

        group.Chance = 0.0001;
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        Assert.That(group.Chance, Is.EqualTo(0.002).Within(0.0000001));

        group.Chance = 0.0005;
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        Assert.That(group.Chance, Is.EqualTo(0.0005).Within(0.0000001), "The host's own chance is kept.");
    }

    private static DropItemGroup GetExcellentGroup(GameConfiguration gameConfiguration)
        => gameConfiguration.DropItemGroups.Single(g => g.ItemType == SpecialItemType.Excellent && g.Monster is null && g.MinimumMonsterLevel is null or 0 && g.ItemLevel == 0 && g.PossibleItems.Count == 0);

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
