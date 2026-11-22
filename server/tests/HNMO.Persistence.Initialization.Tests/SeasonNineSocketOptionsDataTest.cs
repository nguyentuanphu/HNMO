// <copyright file="SeasonNineSocketOptionsDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.Initialization.Updates.SeasonNine;

/// <summary>
/// Verifies <see cref="SeasonNineSocketOptionsUpdatePlugIn"/> updates Season 9 socket option values only in Season 9
/// (including Water shield protection lv5 = 30 %, Wind max AG = 50..130, and Wind AG value = 5..13) while leaving Season 6 intact.
/// </summary>
[TestFixture]
internal class SeasonNineSocketOptionsDataTest
{
    private static readonly float[] ExpectedWindMaxAg = [50, 70, 90, 110, 130];
    private static readonly float[] ExpectedWindAgRecovery = [5, 7, 9, 11, 13];
    private static readonly float[] SeasonSixWindMaxAg = [25, 30, 35, 40, 50];
    private static readonly float[] SeasonSixWindAgRecovery = [3, 5, 7, 10, 15];

    /// <summary>
    /// Verifies Season 9 updates Water level 5 shield protection and Wind AG options after the Season 6 plug-in ran.
    /// </summary>
    [Test]
    public async Task SeasonNineModeAppliesSeasonNineSocketValuesAsync()
    {
        var (context, config) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        var s6PlugIn = new SocketShieldProtectionUpdatePlugIn();
        var s9PlugIn = new SeasonNineSocketOptionsUpdatePlugIn(() => true);

        await s6PlugIn.ApplyUpdateAsync(context, config).ConfigureAwait(false);
        await s9PlugIn.ApplyUpdateAsync(context, config).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(s9PlugIn.DependsOn.Select(d => d.Key), Does.Contain(s6PlugIn.Key));
            Assert.That(GetLevelValue(config, SocketSubOptionType.Water, 2, Stats.ShieldItemDefenseIncrease, 5), Is.EqualTo(0.30f).Within(0.0001f));
            for (int level = 1; level <= 5; level++)
            {
                Assert.That(GetLevelValue(config, SocketSubOptionType.Wind, 4, Stats.MaximumAbility, level), Is.EqualTo(ExpectedWindMaxAg[level - 1]).Within(0.0001f));
                Assert.That(GetLevelValue(config, SocketSubOptionType.Wind, 5, Stats.AbilityRecoveryAbsolute, level), Is.EqualTo(ExpectedWindAgRecovery[level - 1]).Within(0.0001f));
            }
        });
    }

    /// <summary>
    /// Verifies Season 6 mode leaves Season 6 socket values unchanged and marks the update installed.
    /// </summary>
    [Test]
    public async Task SeasonSixModeKeepsSeasonSixSocketValuesAsync()
    {
        var (context, config) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        var s9PlugIn = new SeasonNineSocketOptionsUpdatePlugIn(() => false);

        Assert.That(s9PlugIn.RunsOnNewDatabase, Is.False);
        await s9PlugIn.ApplyUpdateAsync(context, config).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(GetLevelValue(config, SocketSubOptionType.Water, 2, Stats.ShieldItemDefenseIncrease, 5), Is.EqualTo(0.20f).Within(0.0001f));
            for (int level = 1; level <= 5; level++)
            {
                Assert.That(GetLevelValue(config, SocketSubOptionType.Wind, 4, Stats.MaximumAbility, level), Is.EqualTo(SeasonSixWindMaxAg[level - 1]).Within(0.0001f));
                Assert.That(GetLevelValue(config, SocketSubOptionType.Wind, 5, Stats.AbilityRecoveryAbsolute, level), Is.EqualTo(SeasonSixWindAgRecovery[level - 1]).Within(0.0001f));
            }
        });

        var entries = await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false);
        Assert.That(entries.Any(e => e.Key == s9PlugIn.Key && e.InstalledAt is not null), Is.True);
    }

    /// <summary>
    /// Verifies customized values are not overwritten in Season 9 mode.
    /// </summary>
    [Test]
    public async Task CustomizedSocketValuesArePreservedAsync()
    {
        var (context, config) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        GetLevelConstant(config, SocketSubOptionType.Water, 2, Stats.ShieldItemDefenseIncrease, 5).Value = 0.25f;
        GetLevelConstant(config, SocketSubOptionType.Wind, 4, Stats.MaximumAbility, 1).Value = 99f;

        await new SeasonNineSocketOptionsUpdatePlugIn(() => true).ApplyUpdateAsync(context, config).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(GetLevelValue(config, SocketSubOptionType.Water, 2, Stats.ShieldItemDefenseIncrease, 5), Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(GetLevelValue(config, SocketSubOptionType.Wind, 4, Stats.MaximumAbility, 1), Is.EqualTo(99f).Within(0.0001f));
            Assert.That(GetLevelValue(config, SocketSubOptionType.Wind, 4, Stats.MaximumAbility, 2), Is.EqualTo(70f).Within(0.0001f));
        });
    }

    private static float GetLevelValue(GameConfiguration config, SocketSubOptionType subType, int number, AttributeDefinition target, int level) =>
        GetLevelConstant(config, subType, number, target, level).Value;

    private static SimpleElement GetLevelConstant(GameConfiguration config, SocketSubOptionType subType, int number, AttributeDefinition target, int level) =>
        config.ItemOptions
            .SelectMany(o => o.PossibleOptions)
            .Where(o => o.OptionType == ItemOptionTypes.SocketOption && o.SubOptionType == (int)subType && o.Number == number)
            .SelectMany(o => o.LevelDependentOptions)
            .First(l => l.Level == level && l.PowerUpDefinition?.TargetAttribute == target)
            .PowerUpDefinition!.Boost!.ConstantValue!;
}
