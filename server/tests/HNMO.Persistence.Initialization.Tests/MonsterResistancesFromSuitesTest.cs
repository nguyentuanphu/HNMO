// <copyright file="MonsterResistancesFromSuitesTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.InMemory;

/// <summary>
/// The monsters have the ice, poison, lightning and fire resistance of the Season 6/9 server suites, and no lightning
/// resistance is stored as water resistance.
/// </summary>
[TestFixture]
internal class MonsterResistancesFromSuitesTest : MonsterValuesUpdateTestBase<MonsterResistancesFromSuitesUpdatePlugIn>
{
    /// <summary>
    /// Some monsters of a new database, with the values of the suites' monster table.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ResistancesAreTheSuiteValuesAsync()
    {
        var gameConfiguration = await CreateConfigurationAsync(new InMemoryPersistenceContextProvider()).ConfigureAwait(false);
        float Resistance(short monster, MonsterValue value) => MonsterValueChange.Get(Monster(gameConfiguration, monster), value);

        Assert.Multiple(() =>
        {
            Assert.That(Resistance(20, MonsterValue.IceResistance), Is.EqualTo(1), "Elite Yeti ice");
            Assert.That(Resistance(20, MonsterValue.PoisonResistance), Is.EqualTo(4), "Elite Yeti poison");
            Assert.That(Resistance(38, MonsterValue.LightningResistance), Is.EqualTo(10), "Balrog lightning");
            Assert.That(Resistance(434, MonsterValue.LightningResistance), Is.EqualTo(25), "Gigantis lightning");

            var withWater = gameConfiguration.Monsters
                .Where(m => m.ObjectKind == NpcObjectKind.Monster && m.Attributes.Any(a => a.AttributeDefinition?.Id == Stats.WaterResistance.Id))
                .Select(m => m.Number)
                .ToList();
            Assert.That(withWater, Is.Empty, "monsters with a water resistance");
        });
    }

    /// <summary>
    /// The Imperial Guardian monsters have no real row in the suites' table: their water resistance becomes their
    /// lightning resistance with the same value.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ImperialGuardianKeepsItsValueAsLightningAsync()
    {
        var gameConfiguration = await CreateConfigurationAsync(new InMemoryPersistenceContextProvider()).ConfigureAwait(false);

        Assert.That(MonsterValueChange.Get(Monster(gameConfiguration, 504), MonsterValue.LightningResistance), Is.EqualTo(29));
    }
}
