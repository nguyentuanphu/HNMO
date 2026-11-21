// <copyright file="JewelDropsByMonsterLevelDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.DataModel.Configuration;
using HNMO.Persistence.Initialization.Updates;

/// <summary>
/// Tests the jewel drops by monster level (Season 6/9 server suites, monster item drop rate table, turned into a
/// chance per kill with HNMO's 30 % item drop chance).
/// </summary>
[TestFixture]
internal class JewelDropsByMonsterLevelDataTest
{
    /// <summary>A new Season 6 database has the groups by monster level on every map with the old group.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseHasTheGroupsByLevelAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;

        AssertGroups(gameConfiguration);
    }

    /// <summary>The update turns the old 0.1 % group into the groups by level, once.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateMovesTheJewelsOnceAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;
        var oldGroup = OldGroup(gameConfiguration);
        foreach (var group in NewGroups(gameConfiguration).ToList())
        {
            foreach (var map in gameConfiguration.Maps)
            {
                map.DropItemGroups.Remove(group);
            }

            gameConfiguration.DropItemGroups.Remove(group);
        }

        foreach (var (group, number) in JewelDropsByMonsterLevelUpdatePlugIn.Jewels)
        {
            oldGroup.PossibleItems.Add(gameConfiguration.Items.Single(i => i.Group == group && i.Number == number));
        }

        oldGroup.Chance = 0.001;

        var update = new JewelDropsByMonsterLevelUpdatePlugIn();
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        AssertGroups(gameConfiguration);
    }

    private static DropItemGroup OldGroup(GameConfiguration gameConfiguration)
        => gameConfiguration.DropItemGroups.Single(g => g.GetId() == GuidHelper.CreateGuid<DropItemGroup>(4));

    private static IEnumerable<DropItemGroup> NewGroups(GameConfiguration gameConfiguration)
    {
        var ids = Enumerable.Range(0, JewelDropsByMonsterLevelUpdatePlugIn.Bands.Count + 5)
            .Select(i => GuidHelper.CreateGuid<DropItemGroup>(JewelDropsByMonsterLevelUpdatePlugIn.GroupNumber, (short)i))
            .ToHashSet();
        return gameConfiguration.DropItemGroups.Where(g => ids.Contains(g.GetId()));
    }

    private static double ChanceFor(GameConfiguration gameConfiguration, byte group, short number, int monsterLevel)
        => NewGroups(gameConfiguration)
            .Where(g => g.PossibleItems.Any(i => i.Group == group && i.Number == number)
                        && (g.MinimumMonsterLevel ?? 0) <= monsterLevel
                        && (g.MaximumMonsterLevel ?? byte.MaxValue) >= monsterLevel)
            .Sum(g => g.Chance);

    private static void AssertGroups(GameConfiguration gameConfiguration)
    {
        var oldGroup = OldGroup(gameConfiguration);
        var newGroups = NewGroups(gameConfiguration).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(oldGroup.Chance, Is.EqualTo(JewelDropsByMonsterLevelUpdatePlugIn.RemainingOldGroupChance));
            Assert.That(oldGroup.PossibleItems.Any(i => JewelDropsByMonsterLevelUpdatePlugIn.Jewels.Contains((i.Group, i.Number))), Is.False, "no jewel left in the old group");
            Assert.That(oldGroup.PossibleItems, Has.Count.EqualTo(5), "angel, imp, uniria, ale, town portal");
            Assert.That(newGroups, Has.Count.EqualTo(JewelDropsByMonsterLevelUpdatePlugIn.Bands.Count + 5));
            Assert.That(newGroups.Select(g => g.ItemType), Is.All.EqualTo(SpecialItemType.Jewel));

            foreach (var map in gameConfiguration.Maps.Where(m => m.DropItemGroups.Contains(oldGroup)))
            {
                Assert.That(newGroups.All(map.DropItemGroups.Contains), Is.True, $"map {map.Number}");
            }

            // Jewel of Life: 0.196 % at level 15, none at level 75 (the suites' table has none from 70 on).
            Assert.That(ChanceFor(gameConfiguration, 14, 16, 15), Is.EqualTo(0.001963).Within(0.0000001));
            Assert.That(ChanceFor(gameConfiguration, 14, 16, 75), Is.EqualTo(0));

            // Jewel of Bless: 0.171 % at level 125, HNMO's old 0.01 % from level 150, none below level 20.
            Assert.That(ChanceFor(gameConfiguration, 14, 13, 125), Is.EqualTo(0.001707).Within(0.000002));
            Assert.That(ChanceFor(gameConfiguration, 14, 13, 200), Is.EqualTo(JewelDropsByMonsterLevelUpdatePlugIn.HighLevelChancePerJewel));
            Assert.That(ChanceFor(gameConfiguration, 14, 13, 15), Is.EqualTo(0));
        });
    }
}
