// <copyright file="EquippedWeaponMasteryLevelTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using HNMO.DataModel.Configuration;
using HNMO.Persistence.InMemory;
using HNMO.Persistence.Initialization.Skills;
using HNMO.Persistence.Initialization.Updates;

/// <summary>
/// Equipped Weapon Mastery (571) goes to master level 10, like the other weapon masteries and the desktop client's master
/// tree (version 2 of <see cref="FinishRageFighterMasterTreePlugIn"/>).
/// </summary>
[TestFixture]
public class EquippedWeaponMasteryLevelTest
{
    /// <summary>A fresh database has level 10.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task AFreshDatabaseHasTenLevelsAsync()
    {
        var (_, gameConfiguration) = await CreateAsync().ConfigureAwait(false);
        Assert.That(MaximumLevel(gameConfiguration), Is.EqualTo(10));
    }

    /// <summary>A database which has version 1 installed and 20 levels is repaired, and only that changes.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task VersionOneIsRepairedWithoutReplayingItAsync()
    {
        var (provider, gameConfiguration) = await CreateAsync().ConfigureAwait(false);
        using var context = provider.CreateNewContext();
        Master(gameConfiguration).MaximumLevel = 20;
        var update = new FinishRageFighterMasterTreePlugIn();
        var installed = (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false)).Single(entry => entry.Key == update.Key);
        installed.Version = 1;
        var attributes = gameConfiguration.Attributes.Count;
        var relationships = gameConfiguration.CharacterClasses.Sum(c => c.AttributeCombinations.Count);

        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(MaximumLevel(gameConfiguration), Is.EqualTo(10));
            Assert.That(gameConfiguration.Attributes, Has.Count.EqualTo(attributes), "version 1 is not replayed");
            Assert.That(gameConfiguration.CharacterClasses.Sum(c => c.AttributeCombinations.Count), Is.EqualTo(relationships));
        });
    }

    /// <summary>Applying version 2 a second time changes nothing.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ASecondApplicationChangesNothingAsync()
    {
        var (provider, gameConfiguration) = await CreateAsync().ConfigureAwait(false);
        using var context = provider.CreateNewContext();
        var update = new FinishRageFighterMasterTreePlugIn();
        var installed = (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false)).Single(entry => entry.Key == update.Key);
        installed.Version = 1;
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        var attributes = gameConfiguration.Attributes.Count;

        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        Assert.That(MaximumLevel(gameConfiguration), Is.EqualTo(10));
        Assert.That(gameConfiguration.Attributes, Has.Count.EqualTo(attributes));
    }

    private static MasterSkillDefinition Master(GameConfiguration gameConfiguration)
        => gameConfiguration.Skills.Single(s => s.Number == (short)SkillNumber.EquippedWeaponMastery).MasterDefinition!;

    private static int MaximumLevel(GameConfiguration gameConfiguration) => Master(gameConfiguration).MaximumLevel;

    private static async Task<(InMemoryPersistenceContextProvider Provider, GameConfiguration Configuration)> CreateAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new VersionSeasonSix.DataInitialization(provider, new NullLoggerFactory()).CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using var context = provider.CreateNewContext();
        return (provider, (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First());
    }
}
