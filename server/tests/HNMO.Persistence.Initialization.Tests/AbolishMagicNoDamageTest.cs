// <copyright file="AbolishMagicNoDamageTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using HNMO.DataModel.Configuration;
using HNMO.Persistence.Initialization.Skills;
using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.InMemory;

/// <summary>
/// The Dark Lord's Abolish Magic (skill 72) isn't a direct hit skill: the server must not attack with it.
/// </summary>
[TestFixture]
internal class AbolishMagicNoDamageTest
{
    /// <summary>A new database has Abolish Magic as a skill which doesn't hit.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseHasNoDamageAsync()
    {
        var gameConfiguration = await CreateConfigurationAsync(new InMemoryPersistenceContextProvider()).ConfigureAwait(false);

        Assert.That(AbolishMagic(gameConfiguration).SkillType, Is.EqualTo(SkillType.Other));
    }

    /// <summary>A save with Abolish Magic as a direct hit skill is changed; applying the update twice changes nothing more.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateChangesAnOldSaveAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var gameConfiguration = await CreateConfigurationAsync(contextProvider).ConfigureAwait(false);
        AbolishMagic(gameConfiguration).SkillType = SkillType.DirectHit;

        var update = new AbolishMagicNoDamageUpdatePlugIn();
        await update.ApplyUpdateAsync(contextProvider.CreateNewContext(), gameConfiguration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(contextProvider.CreateNewContext(), gameConfiguration).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(AbolishMagic(gameConfiguration).SkillType, Is.EqualTo(SkillType.Other));
            Assert.That(update.IsMandatory, Is.True);
        });
    }

    /// <summary>A skill type the host chose stays.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task HostChoiceIsKeptAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var gameConfiguration = await CreateConfigurationAsync(contextProvider).ConfigureAwait(false);
        AbolishMagic(gameConfiguration).SkillType = SkillType.Buff;

        await new AbolishMagicNoDamageUpdatePlugIn().ApplyUpdateAsync(contextProvider.CreateNewContext(), gameConfiguration).ConfigureAwait(false);

        Assert.That(AbolishMagic(gameConfiguration).SkillType, Is.EqualTo(SkillType.Buff));
    }

    private static Skill AbolishMagic(GameConfiguration gameConfiguration) => gameConfiguration.Skills.Single(s => s.Number == (short)SkillNumber.AbolishMagic);

    private static async Task<GameConfiguration> CreateConfigurationAsync(InMemoryPersistenceContextProvider contextProvider)
    {
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using var context = contextProvider.CreateNewContext();
        return (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
    }
}
