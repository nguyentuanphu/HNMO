// <copyright file="MonsterSpeedsAndRangesFromSuitesTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.InMemory;

/// <summary>
/// The monsters have the move speed, attack speed, view range, attack range and move range of the Season 6/9 server
/// suites.
/// </summary>
[TestFixture]
internal class MonsterSpeedsAndRangesFromSuitesTest : MonsterValuesUpdateTestBase<MonsterSpeedsAndRangesFromSuitesUpdatePlugIn>
{
    /// <summary>
    /// Some monsters of a new database, with the values of the suites' monster table.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SpeedsAndRangesAreTheSuiteValuesAsync()
    {
        var gameConfiguration = await CreateConfigurationAsync(new InMemoryPersistenceContextProvider()).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(Monster(gameConfiguration, 355).AttackRange, Is.EqualTo(4), "Kentauros attack range");
            Assert.That(Monster(gameConfiguration, 355).ViewRange, Is.EqualTo(5), "Kentauros view range");
            Assert.That(Monster(gameConfiguration, 146).AttackRange, Is.EqualTo(6), "Blood Soldier 1 attack range");
            Assert.That(Monster(gameConfiguration, 348).MoveRange, Is.EqualTo(6), "Tanker move range");
            Assert.That(Monster(gameConfiguration, 569).MoveDelay, Is.EqualTo(TimeSpan.FromMilliseconds(900)), "Venomous Chain Scorpion move speed");
            Assert.That(Monster(gameConfiguration, 135).AttackDelay, Is.EqualTo(TimeSpan.FromMilliseconds(2200)), "White Wizard attack speed");
        });
    }
}
