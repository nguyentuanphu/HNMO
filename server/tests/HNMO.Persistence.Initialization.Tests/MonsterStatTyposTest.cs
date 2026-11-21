// <copyright file="MonsterStatTyposTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.InMemory;

/// <summary>
/// Five monster stats had an extra zero: ten times the value of the Season 6/9 server suites and of the
/// neighbouring monsters of the same event.
/// </summary>
[TestFixture]
internal class MonsterStatTyposTest
{
    /// <summary>A new database has the values of the Season 6/9 server suites.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseHasTheSuiteValuesAsync()
    {
        var gameConfiguration = await CreateConfigurationAsync(new InMemoryPersistenceContextProvider()).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(Value(gameConfiguration, 168, Stats.MaximumHealth), Is.EqualTo(6500), "Chaos Castle monster 7 health");
            Assert.That(Value(gameConfiguration, 426, Stats.DefenseRatePvm), Is.EqualTo(190), "Chaos Castle monster 13 defense rate");
            Assert.That(Value(gameConfiguration, 184, Stats.DefenseRatePvm), Is.EqualTo(60), "Blood Soldier 3 defense rate");
            Assert.That(Value(gameConfiguration, 197, Stats.AttackRatePvm), Is.EqualTo(780), "Illusion of Kundun 4 attack rate");
            Assert.That(Value(gameConfiguration, 574, Stats.AttackRatePvm), Is.EqualTo(1140), "Crypos attack rate");
        });
    }

    /// <summary>A save with the wrong values is changed; applying the update twice changes nothing more.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateChangesAnOldSaveAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var gameConfiguration = await CreateConfigurationAsync(contextProvider).ConfigureAwait(false);
        foreach (var fix in FixMonsterStatTyposUpdatePlugIn.Fixes)
        {
            SetValue(gameConfiguration, fix.Monster, fix.Attribute, fix.Wrong);
        }

        var update = new FixMonsterStatTyposUpdatePlugIn();
        await update.ApplyUpdateAsync(contextProvider.CreateNewContext(), gameConfiguration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(contextProvider.CreateNewContext(), gameConfiguration).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            foreach (var fix in FixMonsterStatTyposUpdatePlugIn.Fixes)
            {
                Assert.That(Value(gameConfiguration, fix.Monster, fix.Attribute), Is.EqualTo(fix.Right), $"monster {fix.Monster}");
            }
        });
    }

    /// <summary>A value the host chose stays.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task HostChoiceIsKeptAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var gameConfiguration = await CreateConfigurationAsync(contextProvider).ConfigureAwait(false);
        SetValue(gameConfiguration, 168, Stats.MaximumHealth, 12345);

        await new FixMonsterStatTyposUpdatePlugIn().ApplyUpdateAsync(contextProvider.CreateNewContext(), gameConfiguration).ConfigureAwait(false);

        Assert.That(Value(gameConfiguration, 168, Stats.MaximumHealth), Is.EqualTo(12345));
    }

    private static float Value(GameConfiguration configuration, short monster, HNMO.AttributeSystem.AttributeDefinition attribute)
        => configuration.Monsters.Single(m => m.Number == monster).Attributes.Single(a => a.AttributeDefinition?.Id == attribute.Id).Value;

    private static void SetValue(GameConfiguration configuration, short monster, HNMO.AttributeSystem.AttributeDefinition attribute, float value)
        => configuration.Monsters.Single(m => m.Number == monster).Attributes.Single(a => a.AttributeDefinition?.Id == attribute.Id).Value = value;

    private static async Task<GameConfiguration> CreateConfigurationAsync(InMemoryPersistenceContextProvider contextProvider)
    {
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using var context = contextProvider.CreateNewContext();
        return (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
    }
}
