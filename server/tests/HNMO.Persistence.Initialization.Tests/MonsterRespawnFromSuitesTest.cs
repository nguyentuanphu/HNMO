// <copyright file="MonsterRespawnFromSuitesTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.InMemory;

/// <summary>
/// The monsters have the respawn time of the Season 6/9 server suites.
/// </summary>
[TestFixture]
internal class MonsterRespawnFromSuitesTest : MonsterValuesUpdateTestBase<MonsterRespawnFromSuitesUpdatePlugIn>
{
    /// <summary>
    /// Some monsters of a new database, with the respawn time of the suites' monster table.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task RespawnTimesAreTheSuiteValuesAsync()
    {
        var gameConfiguration = await CreateConfigurationAsync(new InMemoryPersistenceContextProvider()).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(Monster(gameConfiguration, 0).RespawnDelay, Is.EqualTo(TimeSpan.FromSeconds(10)), "Bull Fighter");
            Assert.That(Monster(gameConfiguration, 18).RespawnDelay, Is.EqualTo(TimeSpan.FromSeconds(150)), "Gorgon");
            Assert.That(Monster(gameConfiguration, 72).RespawnDelay, Is.EqualTo(TimeSpan.FromSeconds(30)), "Phantom Knight");
            Assert.That(Monster(gameConfiguration, 77).RespawnDelay, Is.EqualTo(TimeSpan.FromSeconds(150)), "Dark Phoenix");
            Assert.That(Monster(gameConfiguration, 386).RespawnDelay, Is.EqualTo(TimeSpan.FromSeconds(10)), "Illusion Temple spirit keeps 10 s");
        });
    }
}
