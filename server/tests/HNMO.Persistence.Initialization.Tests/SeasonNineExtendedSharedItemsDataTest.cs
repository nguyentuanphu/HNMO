// <copyright file="SeasonNineExtendedSharedItemsDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.Initialization.Updates.SeasonNine;

/// <summary>
/// Verifies <see cref="SeasonNineExtendedSharedItemsUpdatePlugIn"/> applies Sphere (4) and Reset Fruit Season 9 overrides
/// only when running as Season 9, while preserving customized values and Season 6 defaults.
/// </summary>
[TestFixture]
internal class SeasonNineExtendedSharedItemsDataTest
{
    private static readonly short[] ResetFruitNumbers = [54, 55, 56, 57, 58];

    /// <summary>
    /// Verifies Season 9 updates Sphere (4) drop level and Reset Fruit durability.
    /// </summary>
    [Test]
    public async Task SeasonNineModeAppliesExtendedSharedItemValuesAsync()
    {
        var (context, config) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        var plugin = new SeasonNineExtendedSharedItemsUpdatePlugIn(() => true);

        await plugin.ApplyUpdateAsync(context, config).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            var sphereFour = FindItem(config, 12, 73);
            Assert.That(sphereFour.DropLevel, Is.EqualTo(132));

            foreach (var number in ResetFruitNumbers)
            {
                var fruit = FindItem(config, 13, number);
                Assert.That(fruit.Durability, Is.EqualTo(10), $"Reset Fruit 13-{number} durability");
            }
        });
    }

    /// <summary>
    /// Verifies Season 6 marks the update installed without changing Sphere (4) or Reset Fruits.
    /// </summary>
    [Test]
    public async Task SeasonSixModeLeavesExtendedSharedItemsUnchangedAsync()
    {
        var (context, config) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        var origSphereDrop = FindItem(config, 12, 73).DropLevel;
        var origFruitDur = FindItem(config, 13, 54).Durability;
        var plugin = new SeasonNineExtendedSharedItemsUpdatePlugIn(() => false);

        Assert.That(plugin.RunsOnNewDatabase, Is.False);
        await plugin.ApplyUpdateAsync(context, config).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(FindItem(config, 12, 73).DropLevel, Is.EqualTo(origSphereDrop));
            Assert.That(FindItem(config, 13, 54).Durability, Is.EqualTo(origFruitDur));
        });

        var entries = await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false);
        Assert.That(entries.Any(e => e.Key == plugin.Key && e.InstalledAt is not null), Is.True);
    }

    /// <summary>
    /// Verifies operator-customized values are preserved in Season 9 mode.
    /// </summary>
    [Test]
    public async Task CustomizedExtendedSharedItemsArePreservedAsync()
    {
        var (context, config) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        FindItem(config, 12, 73).DropLevel = 99;
        FindItem(config, 13, 54).Durability = 25;

        var plugin = new SeasonNineExtendedSharedItemsUpdatePlugIn(() => true);
        await plugin.ApplyUpdateAsync(context, config).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(FindItem(config, 12, 73).DropLevel, Is.EqualTo(99));
            Assert.That(FindItem(config, 13, 54).Durability, Is.EqualTo(25));
            Assert.That(FindItem(config, 13, 55).Durability, Is.EqualTo(10));
        });
    }

    private static ItemDefinition FindItem(GameConfiguration config, byte group, short number) =>
        config.Items.First(i => i.Group == group && i.Number == number);
}
