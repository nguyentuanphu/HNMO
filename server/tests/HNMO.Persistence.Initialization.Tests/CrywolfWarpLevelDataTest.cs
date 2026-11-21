// <copyright file="CrywolfWarpLevelDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.Persistence.Initialization.Updates;

/// <summary>
/// Tests the level of the Crywolf warp (Season 6 server suite, move list: 300), and that Loren Market stays.
/// </summary>
[TestFixture]
internal class CrywolfWarpLevelDataTest
{
    /// <summary>A new Season 6 database needs level 300 for Crywolf; Loren Market is still in the list.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseNeedsLevel300Async()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;

        Assert.That(gameConfiguration.WarpList.Single(w => w.Index == CrywolfWarpLevelUpdatePlugIn.WarpIndex).LevelRequirement, Is.EqualTo(300));
        Assert.That(gameConfiguration.WarpList.Any(w => w.Index == 44), Is.True, "Loren Market stays");
    }

    /// <summary>The update changes the old 190; a level the host changed stays.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateChangesTheOldLevelAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;
        var warp = gameConfiguration.WarpList.Single(w => w.Index == CrywolfWarpLevelUpdatePlugIn.WarpIndex);
        warp.LevelRequirement = 190;
        var update = new CrywolfWarpLevelUpdatePlugIn();

        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        Assert.That(warp.LevelRequirement, Is.EqualTo(300));

        warp.LevelRequirement = 250;
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        Assert.That(warp.LevelRequirement, Is.EqualTo(250), "the host's own level stays");
    }
}
