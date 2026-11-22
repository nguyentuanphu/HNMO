// <copyright file="SkillEnergyRequirementsTest.cs" company="HNMO">
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
/// Verifies runtime skill requirement checks enforce the Season 6 and Season 9 energy requirements
/// for Recovery (168) and Power Slash (100).
/// </summary>
[TestFixture]
public class SkillEnergyRequirementsTest
{
    private GameConfiguration _gameConfiguration = null!;

    /// <summary>Creates the Season 6 configuration once for all tests.</summary>
    [OneTimeSetUp]
    public async Task SetUpAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new Persistence.Initialization.VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = contextProvider.CreateNewContext();
        this._gameConfiguration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
    }

    /// <summary>Recovery requires 168 energy (not the old 37), matching the Crystal of Recovery.</summary>
    [Test]
    public async ValueTask RecoveryRequires168EnergyAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var recovery = this._gameConfiguration.Skills.Single(s => s.Number == (short)SkillNumber.Recovery);
        player.Attributes![Stats.Level] = 100;

        player.Attributes[Stats.BaseEnergy] = 37;
        Assert.That(player.CompliesRequirements(recovery), Is.False);

        player.Attributes[Stats.BaseEnergy] = 167;
        Assert.That(player.CompliesRequirements(recovery), Is.False);

        player.Attributes[Stats.BaseEnergy] = 168;
        Assert.That(player.CompliesRequirements(recovery), Is.True);
    }

    /// <summary>Power Slash requires 100 energy at runtime.</summary>
    [Test]
    public async ValueTask PowerSlashRequires100EnergyAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var powerSlash = this._gameConfiguration.Skills.Single(s => s.Number == (short)SkillNumber.PowerSlash);

        player.Attributes![Stats.BaseEnergy] = 99;
        Assert.That(player.CompliesRequirements(powerSlash), Is.False);

        player.Attributes[Stats.BaseEnergy] = 100;
        Assert.That(player.CompliesRequirements(powerSlash), Is.True);
    }
}
