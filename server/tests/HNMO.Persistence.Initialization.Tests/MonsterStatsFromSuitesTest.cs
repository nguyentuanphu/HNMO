// <copyright file="MonsterStatsFromSuitesTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.InMemory;

/// <summary>
/// The monsters have the level, health, damage, defense, attack and defense rate of the Season 6/9 server suites:
/// Blood Castle 1-8, the Kanturu bosses, Raklion, Chaos Castle, Kalima, the golden monsters and some others.
/// </summary>
[TestFixture]
internal class MonsterStatsFromSuitesTest : MonsterValuesUpdateTestBase<MonsterStatsFromSuitesUpdatePlugIn>
{
    /// <summary>
    /// Some monsters of a new database, with the values of the suites' monster table.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task EventMonstersHaveTheSuiteStatsAsync()
    {
        var gameConfiguration = await CreateConfigurationAsync(new InMemoryPersistenceContextProvider()).ConfigureAwait(false);
        float Value(short monster, HNMO.AttributeSystem.AttributeDefinition attribute)
            => Monster(gameConfiguration, monster).Attributes.Single(a => a.AttributeDefinition?.Id == attribute.Id).Value;

        Assert.Multiple(() =>
        {
            Assert.That(Value(84, Stats.Level), Is.EqualTo(33), "Chief Skeleton Warrior 1 (Blood Castle 1) level");
            Assert.That(Value(84, Stats.MaximumHealth), Is.EqualTo(1100), "Chief Skeleton Warrior 1 health");
            Assert.That(Value(138, Stats.Level), Is.EqualTo(85), "Chief Skeleton Warrior 7 (Blood Castle 7) level");
            Assert.That(Value(138, Stats.MaximumHealth), Is.EqualTo(23000), "Chief Skeleton Warrior 7 health");
            Assert.That(Value(428, Stats.Level), Is.EqualTo(105), "Chief Skeleton Warrior 8 (Blood Castle 8) level");
            Assert.That(Value(428, Stats.MaximumHealth), Is.EqualTo(29000), "Chief Skeleton Warrior 8 health");
            Assert.That(Value(361, Stats.DefenseBase), Is.EqualTo(1000), "Nightmare defense");
            Assert.That(Value(434, Stats.MaximumHealth), Is.EqualTo(30000), "Gigantis (Raklion) health");
            Assert.That(Value(440, Stats.MaximumHealth), Is.EqualTo(75000), "Dark Elf (Raklion) health");
            Assert.That(Value(44, Stats.Level), Is.EqualTo(60), "Red Dragon (defined with 0.95d) level");
        });
    }

    /// <summary>
    /// The optional update which swapped the stats of Blood Castle 7 and 8 is gone: it would overwrite the suites' values.
    /// </summary>
    [Test]
    public void BloodCastleSwapUpdateIsGone()
    {
        var swapUpdate = new Guid("D4E5F6A0-1B2C-3D4E-5F6A-7B8C9D0E1F2A");
        var updateTypes = typeof(UpdatePlugInBase).Assembly.GetTypes()
            .Where(type => typeof(UpdatePlugInBase).IsAssignableFrom(type) && !type.IsAbstract);

        Assert.That(updateTypes.Select(type => type.GUID), Does.Not.Contain(swapUpdate));
    }
}
