// <copyright file="RaklionSwampGateLevelsDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.DataModel.Configuration;
using HNMO.Persistence.Initialization.Updates;

/// <summary>
/// Tests the level requirements of the Raklion gates (270) and of the gates inside the Swamp of Peace (400),
/// as in the gate settings of the Season 6/9 server suites.
/// </summary>
[TestFixture]
internal class RaklionSwampGateLevelsDataTest
{
    /// <summary>A new Season 6 database has the suites' levels.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseHasTheSuitesLevelsAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;

        AssertLevels(gameConfiguration);
    }

    /// <summary>The update changes the old levels; a level the host changed stays; twice is safe.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateChangesTheOldLevelsAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;
        foreach (var (number, oldLevel, _) in RaklionSwampGateLevelsUpdatePlugIn.Changes)
        {
            GetGate(gameConfiguration, number).LevelRequirement = oldLevel;
        }

        GetGate(gameConfiguration, 292).LevelRequirement = 300;
        var update = new RaklionSwampGateLevelsUpdatePlugIn();
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        Assert.That(GetGate(gameConfiguration, 292).LevelRequirement, Is.EqualTo(300), "the host's own level stays");
        GetGate(gameConfiguration, 292).LevelRequirement = 270;
        AssertLevels(gameConfiguration);
    }

    private static void AssertLevels(GameConfiguration gameConfiguration)
    {
        Assert.Multiple(() =>
        {
            foreach (var (number, _, newLevel) in RaklionSwampGateLevelsUpdatePlugIn.Changes)
            {
                Assert.That(GetGate(gameConfiguration, number).LevelRequirement, Is.EqualTo(newLevel), $"gate {number}");
            }
        });
    }

    private static EnterGate GetGate(GameConfiguration gameConfiguration, short number)
        => gameConfiguration.Maps.SelectMany(m => m.EnterGates).Single(g => g.Number == number);
}
