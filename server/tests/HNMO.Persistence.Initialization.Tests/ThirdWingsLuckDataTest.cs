// <copyright file="ThirdWingsLuckDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.ItemCrafting;
using HNMO.Persistence.Initialization.Updates;

/// <summary>
/// Tests the luck chance of the 3rd level wings mix (Season 6 server suite, chaos box settings: Wing3LuckRate 10).
/// </summary>
[TestFixture]
internal class ThirdWingsLuckDataTest
{
    /// <summary>A new Season 6 database gives luck at 10 %.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseGivesLuckAtTenPercentAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;

        Assert.That(GetSettings(gameConfiguration).ResultItemLuckOptionChance, Is.EqualTo(10));
    }

    /// <summary>The update changes the old 5 %; a chance the host changed stays.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateChangesTheOldChanceAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;
        var settings = GetSettings(gameConfiguration);
        settings.ResultItemLuckOptionChance = 5;
        var update = new ThirdWingsLuckUpdatePlugIn();

        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        Assert.That(settings.ResultItemLuckOptionChance, Is.EqualTo(10));

        settings.ResultItemLuckOptionChance = 7;
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        Assert.That(settings.ResultItemLuckOptionChance, Is.EqualTo(7), "the host's own chance stays");
    }

    private static SimpleCraftingSettings GetSettings(GameConfiguration gameConfiguration)
        => gameConfiguration.Monsters.Where(m => m.NpcWindow == NpcWindow.ChaosMachine)
               .SelectMany(m => m.ItemCraftings)
               .First(c => c.Number == ThirdWingsLuckUpdatePlugIn.CraftingNumber)
               .SimpleCraftingSettings!;
}
