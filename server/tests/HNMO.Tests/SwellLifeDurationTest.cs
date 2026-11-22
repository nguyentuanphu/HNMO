// <copyright file="SwellLifeDurationTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.Skills;
using HNMO.Persistence.InMemory;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Verifies runtime Swell Life and Swell Life Proficiency durations compute <c>60 + Energy / 10</c> s
/// with no 180-s cap, matching the Season 6 server suite.
/// </summary>
[TestFixture]
public class SwellLifeDurationTest
{
    private GameConfiguration _gameConfiguration = null!;

    /// <summary>Creates the Season 6 configuration once for all tests.</summary>
    [OneTimeSetUp]
    public async Task SetUpAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new Persistence.Initialization.VersionSeasonSix.DataInitialization(provider, new NullLoggerFactory())
            .CreateInitialDataAsync(1, false)
            .ConfigureAwait(false);
        using var context = provider.CreateNewContext();
        this._gameConfiguration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
    }

    /// <summary>Swell Life and its proficiency buff last 60 + Energy / 10 seconds with no cap above 180 s.</summary>
    /// <param name="energy">Total energy of the caster.</param>
    /// <param name="expectedSeconds">Expected buff duration in seconds.</param>
    [TestCase(100, 70f)]
    [TestCase(1200, 180f)]
    [TestCase(2000, 260f)]
    [TestCase(5000, 560f)]
    public async ValueTask SwellLifeDurationMatchesSuiteFormulaAsync(int energy, float expectedSeconds)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Attributes![Stats.BaseEnergy] = energy;

        foreach (var number in new[] { MagicEffectNumber.GreaterFortitude, MagicEffectNumber.GreaterFortitudeProficiency })
        {
            var effect = this._gameConfiguration.MagicEffects.Single(e => e.Number == (short)number);
            var duration = player.Attributes.CreateDurationElement(effect.Duration!).Value;
            Assert.That(duration, Is.EqualTo(expectedSeconds).Within(0.01f), number.ToString());
        }
    }
}
