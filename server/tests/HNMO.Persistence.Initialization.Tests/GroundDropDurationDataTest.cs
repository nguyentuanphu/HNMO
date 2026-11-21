// <copyright file="GroundDropDurationDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using HNMO.DataModel.Configuration;
using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.InMemory;

/// <summary>
/// Tests the time drops stay on the ground: items 20 s, zen 15 s (Season 9 server suites).
/// </summary>
[TestFixture]
internal class GroundDropDurationDataTest
{
    /// <summary>
    /// A new season 6 database keeps items 20 s and zen 15 s.
    /// </summary>
    [Test]
    public async Task NewDatabaseHasTheDurationsAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;

        Assert.Multiple(() =>
        {
            Assert.That(gameConfiguration.ItemDropDuration, Is.EqualTo(TimeSpan.FromSeconds(20)));
            Assert.That(gameConfiguration.MoneyDropDuration, Is.EqualTo(TimeSpan.FromSeconds(15)));
        });
    }

    /// <summary>
    /// The update shortens the old 60 s to 20 s and sets a missing zen time; a time the host changed is kept.
    /// </summary>
    [Test]
    public async Task UpdateShortensTheOldDurationOnlyAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;
        var update = new GroundDropDurationUpdatePlugIn();

        gameConfiguration.ItemDropDuration = TimeSpan.FromSeconds(60);
        gameConfiguration.MoneyDropDuration = TimeSpan.Zero;
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        Assert.Multiple(() =>
        {
            Assert.That(gameConfiguration.ItemDropDuration, Is.EqualTo(TimeSpan.FromSeconds(20)));
            Assert.That(gameConfiguration.MoneyDropDuration, Is.EqualTo(TimeSpan.FromSeconds(15)));
        });

        gameConfiguration.ItemDropDuration = TimeSpan.FromSeconds(45);
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        Assert.That(gameConfiguration.ItemDropDuration, Is.EqualTo(TimeSpan.FromSeconds(45)), "The host's own time is kept.");
    }

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
