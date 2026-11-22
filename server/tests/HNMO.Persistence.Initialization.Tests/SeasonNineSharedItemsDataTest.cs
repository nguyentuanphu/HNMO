// <copyright file="SeasonNineSharedItemsDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.Initialization.Updates.SeasonNine;

/// <summary>
/// Verifies <see cref="SeasonNineSharedItemsUpdatePlugIn"/> updates Season 9 shared item values only in Season 9
/// and preserves customized values and Season 6 data.
/// </summary>
[TestFixture]
internal class SeasonNineSharedItemsDataTest
{
    private static readonly short[] PotionNumbers = [0, 1, 2, 3, 4, 5, 6];

    /// <summary>
    /// Verifies Season 9 updates Cape of Fighter drop level, Cape of Lord level requirement, and basic potion stacks.
    /// </summary>
    [Test]
    public async Task SeasonNineModeAppliesSeasonNineSharedItemValuesAsync()
    {
        var (context, config) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        var plugin = new SeasonNineSharedItemsUpdatePlugIn(() => true);

        await plugin.ApplyUpdateAsync(context, config).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            var capeOfFighter = FindItem(config, 12, 49);
            Assert.That(capeOfFighter.DropLevel, Is.EqualTo(150));

            var capeOfLord = FindItem(config, 13, 30);
            var levelReq = capeOfLord.Requirements.First(r => r.Attribute == Stats.Level);
            Assert.That(levelReq.MinimumValue, Is.EqualTo(150));

            foreach (var number in PotionNumbers)
            {
                var potion = FindItem(config, 14, number);
                Assert.That(potion.Durability, Is.EqualTo(255), $"Potion 14-{number} stack limit");
            }
        });
    }

    /// <summary>
    /// Verifies Season 6 marks the update installed without modifying any Season 6 item values.
    /// </summary>
    [Test]
    public async Task SeasonSixModeLeavesItemsUnchangedAndRecordsUpdateAsync()
    {
        var (context, config) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        var plugin = new SeasonNineSharedItemsUpdatePlugIn(() => false);

        Assert.That(plugin.RunsOnNewDatabase, Is.False);
        await plugin.ApplyUpdateAsync(context, config).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(FindItem(config, 12, 49).DropLevel, Is.EqualTo(180));
            Assert.That(FindItem(config, 13, 30).Requirements.First(r => r.Attribute == Stats.Level).MinimumValue, Is.EqualTo(180));
            foreach (var number in PotionNumbers)
            {
                Assert.That(FindItem(config, 14, number).Durability, Is.EqualTo(3));
            }
        });

        var entries = await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false);
        Assert.That(entries.Any(e => e.Key == plugin.Key && e.InstalledAt is not null), Is.True);
    }

    /// <summary>
    /// Verifies operator-customized values are not overwritten when applying the Season 9 update.
    /// </summary>
    [Test]
    public async Task CustomizedValuesArePreservedInSeasonNineModeAsync()
    {
        var (context, config) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        FindItem(config, 12, 49).DropLevel = 165;
        FindItem(config, 13, 30).Requirements.First(r => r.Attribute == Stats.Level).MinimumValue = 175;
        FindItem(config, 14, 0).Durability = 50;

        var plugin = new SeasonNineSharedItemsUpdatePlugIn(() => true);
        await plugin.ApplyUpdateAsync(context, config).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(FindItem(config, 12, 49).DropLevel, Is.EqualTo(165));
            Assert.That(FindItem(config, 13, 30).Requirements.First(r => r.Attribute == Stats.Level).MinimumValue, Is.EqualTo(175));
            Assert.That(FindItem(config, 14, 0).Durability, Is.EqualTo(50));
            Assert.That(FindItem(config, 14, 1).Durability, Is.EqualTo(255));
        });
    }

    private static ItemDefinition FindItem(GameConfiguration config, byte group, short number) =>
        config.Items.First(i => i.Group == group && i.Number == number);
}
