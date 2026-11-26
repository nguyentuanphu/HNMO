// <copyright file="BlessPotionSiegeOnlyTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using HNMO.DataModel.Attributes;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.Skills;
using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.InMemory;

/// <summary>
/// The Potion of Bless raises the damage only against Castle Siege gates and statues, not through a power-up.
/// </summary>
[TestFixture]
internal class BlessPotionSiegeOnlyTest
{
    /// <summary>A new database has the effect without a damage power-up.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseHasNoPowerUpAsync()
    {
        var gameConfiguration = await CreateConfigurationAsync(new InMemoryPersistenceContextProvider()).ConfigureAwait(false);

        Assert.That(Effect(gameConfiguration).PowerUpDefinitions, Is.Empty);
    }

    /// <summary>An old save's power-up is removed; applying the update twice changes nothing more.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateRemovesThePowerUpOfAnOldSaveAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var gameConfiguration = await CreateConfigurationAsync(contextProvider).ConfigureAwait(false);
        using var context = contextProvider.CreateNewContext();
        var powerUp = context.CreateNew<PowerUpDefinition>();
        powerUp.TargetAttribute = Stats.AttackDamageIncrease.GetPersistent(gameConfiguration);
        Effect(gameConfiguration).PowerUpDefinitions.Add(powerUp);

        var update = new BlessPotionSiegeOnlyUpdatePlugIn();
        await update.ApplyUpdateAsync(contextProvider.CreateNewContext(), gameConfiguration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(contextProvider.CreateNewContext(), gameConfiguration).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(Effect(gameConfiguration).PowerUpDefinitions, Is.Empty);
            Assert.That(update.IsMandatory, Is.True);
        });
    }

    private static MagicEffectDefinition Effect(GameConfiguration gameConfiguration) => gameConfiguration.MagicEffects.Single(m => m.Number == (short)MagicEffectNumber.PotionOfBless);

    private static async Task<GameConfiguration> CreateConfigurationAsync(InMemoryPersistenceContextProvider contextProvider)
    {
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using var context = contextProvider.CreateNewContext();
        return (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
    }
}
