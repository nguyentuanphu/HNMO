// <copyright file="PlusItemUpgradeRatesDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.ItemCrafting;
using HNMO.Persistence.Initialization.Updates;

/// <summary>
/// Tests the base success rates of the chaos machine's item upgrades +10 .. +15
/// (Season 6/9 server suites, chaos box settings: 60, 60, 60, 55, 55, 50; luck +25).
/// </summary>
[TestFixture]
internal class PlusItemUpgradeRatesDataTest
{
    private static readonly (byte Number, byte TargetLevel, byte Rate)[] Expected =
    [
        (3, 10, 60),
        (4, 11, 60),
        (22, 12, 60),
        (23, 13, 55),
        (49, 14, 55),
        (50, 15, 50),
    ];

    /// <summary>A new Season 6 database has the suites' rates.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseHasTheSuitesRatesAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;

        AssertRates(gameConfiguration);
    }

    /// <summary>The update changes the old +12 and +14 rates; a rate the host changed stays; twice is safe.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateChangesTheOldRatesAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;
        GetSettings(gameConfiguration, 22).SuccessPercent = 55;
        GetSettings(gameConfiguration, 49).SuccessPercent = 50;
        var update = new PlusItemUpgradeRatesUpdatePlugIn();

        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        AssertRates(gameConfiguration);

        GetSettings(gameConfiguration, 22).SuccessPercent = 70;
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        Assert.That(GetSettings(gameConfiguration, 22).SuccessPercent, Is.EqualTo(70), "the host's own rate stays");
    }

    private static void AssertRates(GameConfiguration gameConfiguration)
    {
        Assert.Multiple(() =>
        {
            foreach (var (number, targetLevel, rate) in Expected)
            {
                var settings = GetSettings(gameConfiguration, number);
                Assert.That(settings.SuccessPercent, Is.EqualTo(rate), $"+{targetLevel}");
                Assert.That(settings.SuccessPercentageAdditionForLuck, Is.EqualTo(25), $"+{targetLevel} luck");
                Assert.That(settings.RequiredItems.Single(r => r.Reference == 1).MinimumItemLevel, Is.EqualTo(targetLevel - 1), $"+{targetLevel} item level");
            }
        });
    }

    private static SimpleCraftingSettings GetSettings(GameConfiguration gameConfiguration, byte number)
        => gameConfiguration.Monsters.Where(m => m.NpcWindow == NpcWindow.ChaosMachine)
               .SelectMany(m => m.ItemCraftings)
               .First(c => c.Number == number)
               .SimpleCraftingSettings
           ?? throw new AssertionException($"No settings for crafting {number}");
}
