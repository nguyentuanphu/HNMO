// <copyright file="KundunFromSuitesTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.InMemory;

/// <summary>
/// Kundun (275, Kalima 7) has the values of the Season 6/9 server suites.
/// </summary>
[TestFixture]
internal class KundunFromSuitesTest : MonsterValuesUpdateTestBase<KundunFromSuitesUpdatePlugIn>
{
    /// <summary>
    /// A new database has Kundun with 5,000,000 health, level 147 and 12 hours to respawn.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task KundunHasTheSuiteValuesAsync()
    {
        var gameConfiguration = await CreateConfigurationAsync(new InMemoryPersistenceContextProvider()).ConfigureAwait(false);
        var kundun = Monster(gameConfiguration, 275);

        Assert.Multiple(() =>
        {
            Assert.That(kundun.Attributes.Single(a => a.AttributeDefinition?.Id == Stats.MaximumHealth.Id).Value, Is.EqualTo(5_000_000));
            Assert.That(kundun.Attributes.Single(a => a.AttributeDefinition?.Id == Stats.Level.Id).Value, Is.EqualTo(147));
            Assert.That(kundun.AttackRange, Is.EqualTo(15));
            Assert.That(kundun.ViewRange, Is.EqualTo(10));
            Assert.That(kundun.RespawnDelay, Is.EqualTo(TimeSpan.FromHours(12)));
        });
    }
}
