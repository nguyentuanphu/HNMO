// <copyright file="ElvenlandMonstersDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.InMemory;

/// <summary>
/// The Elvenland monsters (418-425) have the values of the Season 6/9 server suites: 419 is the Hideous
/// Rabbit, 420 the Werewolf and 421 the Polluted Butterfly, as in both clients.
/// </summary>
[TestFixture]
internal class ElvenlandMonstersDataTest
{
    /// <summary>A new database has the names and values of the suites.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseHasTheSuiteValuesAsync()
    {
        var gameConfiguration = await CreateConfigurationAsync(new InMemoryPersistenceContextProvider()).ConfigureAwait(false);

        AssertSuiteValues(gameConfiguration);
    }

    /// <summary>A save with the old values gets the values of the suites.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateChangesAnOldSaveAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var gameConfiguration = await CreateConfigurationAsync(contextProvider).ConfigureAwait(false);
        var butterfly = Monster(gameConfiguration, 419);
        butterfly.Designation = "Polluted Butterfly";
        butterfly.ViewRange = 5;
        butterfly.AttackDelay = TimeSpan.FromMilliseconds(1500);
        Attribute(butterfly, Stats.Level).Value = 13;
        Attribute(butterfly, Stats.AttackRatePvm).Value = 10;
        Attribute(Monster(gameConfiguration, 422), Stats.AttackRatePvm).Value = 10;
        Monster(gameConfiguration, 422).AttackRange = 1;

        await new FixElvenlandMonstersUpdatePlugIn().ApplyUpdateAsync(contextProvider.CreateNewContext(), gameConfiguration).ConfigureAwait(false);

        AssertSuiteValues(gameConfiguration);
    }

    private static void AssertSuiteValues(GameConfiguration gameConfiguration)
    {
        Assert.Multiple(() =>
        {
            Assert.That(Monster(gameConfiguration, 419).Designation.ValueInNeutralLanguage, Is.EqualTo("Hideous Rabbit"));
            Assert.That(Monster(gameConfiguration, 420).Designation.ValueInNeutralLanguage, Is.EqualTo("Werewolf"));
            Assert.That(Monster(gameConfiguration, 421).Designation.ValueInNeutralLanguage, Is.EqualTo("Polluted Butterfly"));
            foreach (var values in FixElvenlandMonstersUpdatePlugIn.Monsters)
            {
                var monster = Monster(gameConfiguration, values.Number);
                var name = $"monster {values.Number}";
                Assert.That(Attribute(monster, Stats.Level).Value, Is.EqualTo(values.Level), name);
                Assert.That(Attribute(monster, Stats.MaximumHealth).Value, Is.EqualTo(values.Health), name);
                Assert.That(Attribute(monster, Stats.MinimumPhysBaseDmg).Value, Is.EqualTo(values.MinimumDamage), name);
                Assert.That(Attribute(monster, Stats.MaximumPhysBaseDmg).Value, Is.EqualTo(values.MaximumDamage), name);
                Assert.That(Attribute(monster, Stats.DefenseBase).Value, Is.EqualTo(values.Defense), name);
                Assert.That(Attribute(monster, Stats.AttackRatePvm).Value, Is.EqualTo(values.AttackRate), name);
                Assert.That(Attribute(monster, Stats.DefenseRatePvm).Value, Is.EqualTo(values.DefenseRate), name);
                Assert.That(monster.MoveRange, Is.EqualTo(values.MoveRange), name);
                Assert.That(monster.AttackRange, Is.EqualTo(values.AttackRange), name);
                Assert.That(monster.ViewRange, Is.EqualTo(values.ViewRange), name);
                Assert.That(monster.AttackDelay, Is.EqualTo(TimeSpan.FromMilliseconds(values.AttackDelayMilliseconds)), name);
                Assert.That(monster.RespawnDelay, Is.EqualTo(TimeSpan.FromSeconds(values.RespawnSeconds)), name);
            }

            // Spot checks against the suites' table, so the test doesn't only compare the update with itself.
            Assert.That(Attribute(Monster(gameConfiguration, 419), Stats.Level).Value, Is.EqualTo(19), "Hideous Rabbit level");
            Assert.That(Attribute(Monster(gameConfiguration, 421), Stats.Level).Value, Is.EqualTo(13), "Polluted Butterfly level");
            Assert.That(Attribute(Monster(gameConfiguration, 425), Stats.AttackRatePvm).Value, Is.EqualTo(240), "Captain Grizzly attack rate");
            Assert.That(Monster(gameConfiguration, 422).AttackRange, Is.EqualTo(4), "Cursed Lich attack range");
        });
    }

    private static MonsterDefinition Monster(GameConfiguration configuration, short number) => configuration.Monsters.Single(m => m.Number == number);

    private static MonsterAttribute Attribute(MonsterDefinition monster, HNMO.AttributeSystem.AttributeDefinition attribute)
        => monster.Attributes.Single(a => a.AttributeDefinition?.Id == attribute.Id);

    private static async Task<GameConfiguration> CreateConfigurationAsync(InMemoryPersistenceContextProvider contextProvider)
    {
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using var context = contextProvider.CreateNewContext();
        return (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
    }
}
