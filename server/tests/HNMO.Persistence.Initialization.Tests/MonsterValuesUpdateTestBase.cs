// <copyright file="MonsterValuesUpdateTestBase.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using HNMO.DataModel.Configuration;
using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.InMemory;

/// <summary>
/// The tests of an update which gives monsters the values of the Season 6/9 server suites.
/// </summary>
/// <typeparam name="TUpdate">The type of the update.</typeparam>
internal abstract class MonsterValuesUpdateTestBase<TUpdate>
    where TUpdate : MonsterValuesUpdatePlugInBase, new()
{
    /// <summary>A new Season 6 database has the suites' value of every change.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseHasTheSuiteValuesAsync()
    {
        var gameConfiguration = await CreateConfigurationAsync(new InMemoryPersistenceContextProvider()).ConfigureAwait(false);
        var update = new TUpdate();
        Assert.That(update.Changes, Is.Not.Empty);

        var wrong = update.Changes
            .Where(change => !change.HasNewValue(Monster(gameConfiguration, change.Monster)))
            .Select(change => $"{change.Monster} {change.Value}: {MonsterValueChange.Get(Monster(gameConfiguration, change.Monster), change.Value)} instead of {change.New}")
            .ToList();
        Assert.That(wrong, Is.Empty);
    }

    /// <summary>An old save gets the suites' values; applying the update twice changes nothing more.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateChangesAnOldSaveAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var gameConfiguration = await CreateConfigurationAsync(contextProvider).ConfigureAwait(false);
        using var context = contextProvider.CreateNewContext();
        var update = new TUpdate();
        foreach (var change in update.Changes)
        {
            MonsterValueChange.Set(Monster(gameConfiguration, change.Monster), change.Value, change.Old, context, gameConfiguration);
        }

        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        var wrong = update.Changes
            .Where(change => !change.HasNewValue(Monster(gameConfiguration, change.Monster)))
            .Select(change => $"{change.Monster} {change.Value}")
            .ToList();
        Assert.That(wrong, Is.Empty);
    }

    /// <summary>A value the host chose stays.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task HostChoiceIsKeptAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var gameConfiguration = await CreateConfigurationAsync(contextProvider).ConfigureAwait(false);
        using var context = contextProvider.CreateNewContext();
        var update = new TUpdate();
        var change = update.Changes.First(c => !float.IsNaN(c.Old) && !float.IsNaN(c.New));
        var monster = Monster(gameConfiguration, change.Monster);
        var hostValue = change.Old + 1 == change.New ? change.Old + 2 : change.Old + 1;
        MonsterValueChange.Set(monster, change.Value, hostValue, context, gameConfiguration);

        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        Assert.That(MonsterValueChange.Get(monster, change.Value), Is.EqualTo(hostValue));
    }

    /// <summary>
    /// Gets the monster definition with the number.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="number">The monster number.</param>
    /// <returns>The monster definition.</returns>
    protected static MonsterDefinition Monster(GameConfiguration configuration, short number)
        => configuration.Monsters.Single(m => m.Number == number);

    /// <summary>
    /// Creates a new Season 6 configuration.
    /// </summary>
    /// <param name="contextProvider">The context provider.</param>
    /// <returns>The configuration.</returns>
    protected static async Task<GameConfiguration> CreateConfigurationAsync(InMemoryPersistenceContextProvider contextProvider)
    {
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using var context = contextProvider.CreateNewContext();
        return (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
    }
}
