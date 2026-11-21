// <copyright file="HarmonyLevels14And15DataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.Persistence.Initialization.Updates;

/// <summary>
/// Tests the jewel of harmony values for items +14 and +15 (Season 6/9 server suites, harmony option table:
/// e.g. weapon minimum damage +23 / +26, armor defense +28 / +31).
/// </summary>
[TestFixture]
internal class HarmonyLevels14And15DataTest
{
    /// <summary>A new Season 6 database has the values of the levels 14 and 15 for every harmony option.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseHasLevels14And15Async()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;

        AssertLevels(gameConfiguration);
    }

    /// <summary>The update adds the levels once.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateAddsTheLevelsOnceAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;
        foreach (var option in HarmonyOptions(gameConfiguration))
        {
            foreach (var level in option.LevelDependentOptions.Where(l => l.Level > 13).ToList())
            {
                option.LevelDependentOptions.Remove(level);
            }
        }

        var update = new HarmonyLevels14And15UpdatePlugIn();
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        AssertLevels(gameConfiguration);
    }

    private static IEnumerable<IncreasableItemOption> HarmonyOptions(GameConfiguration gameConfiguration)
        => gameConfiguration.ItemOptions.SelectMany(d => d.PossibleOptions).Where(o => o.OptionType == ItemOptionTypes.HarmonyOption).Distinct();

    private static void AssertLevels(GameConfiguration gameConfiguration)
    {
        var options = HarmonyOptions(gameConfiguration).ToList();
        Assert.That(options, Has.Count.EqualTo(26), "10 weapon, 8 staff, 8 armor options");
        Assert.Multiple(() =>
        {
            foreach (var option in options)
            {
                var levels = option.LevelDependentOptions.Select(l => l.Level).OrderBy(l => l).ToList();
                Assert.That(levels.Count(l => l == 14), Is.EqualTo(1), $"option {option.Number} level 14");
                Assert.That(levels.Count(l => l == 15), Is.EqualTo(1), $"option {option.Number} level 15");
                var thirteen = option.LevelDependentOptions.Single(l => l.Level == 13).PowerUpDefinition!;
                var fifteen = option.LevelDependentOptions.Single(l => l.Level == 15);
                Assert.That(fifteen.RequiredItemLevel, Is.EqualTo(15));
                Assert.That(fifteen.PowerUpDefinition!.TargetAttribute, Is.EqualTo(thirteen.TargetAttribute));
                Assert.That(fifteen.PowerUpDefinition.Boost!.ConstantValue.Value, Is.GreaterThan(thirteen.Boost!.ConstantValue.Value), $"option {option.Number} grows");
            }
        });

        var weaponMinimumDamage = options.Single(o => o.GetId() == GuidHelper.CreateGuid<IncreasableItemOption>(ItemOptionDefinitionNumbers.HarmonyPhysical, 1));
        Assert.That(weaponMinimumDamage.LevelDependentOptions.Single(l => l.Level == 15).PowerUpDefinition!.Boost!.ConstantValue.Value, Is.EqualTo(26));
    }
}
