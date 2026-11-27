// <copyright file="S6EventsDataTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.DataModel.Configuration;
using HNMO.Persistence.InMemory;
using HNMO.Persistence.Initialization.Updates;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Verifies Season 6 event monsters (300-303, 466, 605-607) and items (0-41, 14-215..216) initialization and update plug-in.
/// </summary>
[TestFixture]
internal class S6EventsDataTests
{
    /// <summary>Verifies freshly initialized Season 6 data includes all Loren Deep, XMas Goblin, Mining, and Mu Rummy definitions.</summary>
    [Test]
    public async Task NewSeason6DatabaseContainsEventMonstersAndItemsAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);

        using var context = contextProvider.CreateNewContext();
        var gameConfiguration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();

        AssertDataPresent(gameConfiguration);
    }

    /// <summary>Verifies <see cref="S6EventsMonstersAndItemsUpdatePlugIn"/> restores removed entries idempotently.</summary>
    [Test]
    public async Task UpdatePlugInRestoresMissingEntriesIdempotentlyAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);

        using var context = contextProvider.CreateNewContext();
        var gameConfiguration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();

        foreach (var number in new short[] { 300, 301, 302, 303, 466, 605, 606, 607 })
        {
            if (gameConfiguration.Monsters.FirstOrDefault(m => m.Number == number) is { } mob)
            {
                gameConfiguration.Monsters.Remove(mob);
            }
        }

        foreach (var (g, n) in new (byte, short)[] { (0, 41), (14, 215), (14, 216) })
        {
            if (gameConfiguration.Items.FirstOrDefault(i => i.Group == g && i.Number == n) is { } item)
            {
                gameConfiguration.Items.Remove(item);
            }
        }

        var update = new S6EventsMonstersAndItemsUpdatePlugIn();
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        var monsterCountAfterFirst = gameConfiguration.Monsters.Count;
        var itemCountAfterFirst = gameConfiguration.Items.Count;
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        Assert.That(gameConfiguration.Monsters.Count, Is.EqualTo(monsterCountAfterFirst));
        Assert.That(gameConfiguration.Items.Count, Is.EqualTo(itemCountAfterFirst));
        AssertDataPresent(gameConfiguration);
    }

    private static void AssertDataPresent(GameConfiguration gameConfiguration)
    {
        Assert.Multiple(() =>
        {
            foreach (var monsterNumber in new short[] { 300, 301, 302, 303, 466, 605, 606, 607 })
            {
                Assert.That(gameConfiguration.Monsters.Any(m => m.Number == monsterNumber), Is.True, $"Monster {monsterNumber}");
            }

            Assert.That(gameConfiguration.Items.Any(i => i.Group == 0 && i.Number == 41), Is.True, "Pandora Pick 0-41");
            Assert.That(gameConfiguration.Items.Any(i => i.Group == 14 && i.Number == 215), Is.True, "Card Piece 14-215");
            Assert.That(gameConfiguration.Items.Any(i => i.Group == 14 && i.Number == 216), Is.True, "Card Deck 14-216");
        });
    }
}
