// <copyright file="BloodCastleDurationDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.DataModel.Configuration;
using HNMO.Persistence.Initialization.Updates;

/// <summary>
/// Tests the Blood Castle times (Season 6/9 server suites: open 5 minutes, play 15 minutes, close 1 minute).
/// </summary>
[TestFixture]
internal class BloodCastleDurationDataTest
{
    /// <summary>A new Season 6 database has the suites' times for all eight castles.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseHasTheSuitesTimesAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;

        var castles = BloodCastles(gameConfiguration);
        Assert.That(castles, Has.Count.EqualTo(8));
        Assert.Multiple(() =>
        {
            foreach (var castle in castles)
            {
                Assert.That(castle.EnterDuration, Is.EqualTo(TimeSpan.FromMinutes(5)), $"{castle.GameLevel} open");
                Assert.That(castle.GameDuration, Is.EqualTo(TimeSpan.FromMinutes(15)), $"{castle.GameLevel} play");
                Assert.That(castle.ExitDuration, Is.EqualTo(TimeSpan.FromMinutes(1)), $"{castle.GameLevel} close");
            }
        });
    }

    /// <summary>The update changes the old 20 minutes; a time the host changed stays; twice is safe.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateChangesTheOldTimeAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;
        var castles = BloodCastles(gameConfiguration);
        castles.ForEach(c => c.GameDuration = TimeSpan.FromMinutes(20));
        castles[0].GameDuration = TimeSpan.FromMinutes(25);

        var update = new BloodCastleDurationUpdatePlugIn();
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        Assert.That(castles[0].GameDuration, Is.EqualTo(TimeSpan.FromMinutes(25)), "the host's own time stays");
        Assert.That(castles.Skip(1).Select(c => c.GameDuration), Is.All.EqualTo(TimeSpan.FromMinutes(15)));
    }

    private static List<MiniGameDefinition> BloodCastles(GameConfiguration gameConfiguration)
        => gameConfiguration.MiniGameDefinitions.Where(d => d.Type == MiniGameType.BloodCastle).OrderBy(d => d.GameLevel).ToList();
}
