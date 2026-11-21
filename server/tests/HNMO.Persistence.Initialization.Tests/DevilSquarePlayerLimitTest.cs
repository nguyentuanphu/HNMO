// <copyright file="DevilSquarePlayerLimitTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using HNMO.DataModel.Configuration;
using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.InMemory;

/// <summary>
/// Every Devil Square lets 15 players in, like the Season 6/9 server suites (owner's choice, 2026-11-20).
/// </summary>
[TestFixture]
internal class DevilSquarePlayerLimitTest
{
    /// <summary>A new Season 6 database lets 15 players into every Devil Square.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseAllowsFifteenPlayersAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using var context = contextProvider.CreateNewContext();
        var gameConfiguration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();

        var devilSquares = gameConfiguration.MiniGameDefinitions.Where(d => d.Type == MiniGameType.DevilSquare).ToList();

        Assert.That(devilSquares, Has.Count.EqualTo(7));
        Assert.That(devilSquares.Select(d => d.MaximumPlayerCount), Has.All.EqualTo(15));
    }

    /// <summary>
    /// The update raises the earlier default of 10 to 15, leaves a limit the host changed and other mini games alone,
    /// and applying it twice changes nothing more.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateRaisesOnlyTheEarlierDefaultAsync()
    {
        var configuration = new HNMO.Persistence.BasicModel.GameConfiguration();
        var oldDefault = Add(configuration, MiniGameType.DevilSquare, 10);
        var hostChoice = Add(configuration, MiniGameType.DevilSquare, 12);
        var bloodCastle = Add(configuration, MiniGameType.BloodCastle, 10);
        var update = new DevilSquarePlayerLimitUpdatePlugIn();

        await update.ApplyUpdateAsync(new InMemoryPersistenceContextProvider().CreateNewContext(), configuration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(new InMemoryPersistenceContextProvider().CreateNewContext(), configuration).ConfigureAwait(false);

        Assert.That(oldDefault.MaximumPlayerCount, Is.EqualTo(15));
        Assert.That(hostChoice.MaximumPlayerCount, Is.EqualTo(12));
        Assert.That(bloodCastle.MaximumPlayerCount, Is.EqualTo(10));
    }

    private static MiniGameDefinition Add(GameConfiguration configuration, MiniGameType type, int maximumPlayerCount)
    {
        var definition = new HNMO.Persistence.BasicModel.MiniGameDefinition { Type = type, MaximumPlayerCount = maximumPlayerCount };
        configuration.MiniGameDefinitions.Add(definition);
        return definition;
    }
}
