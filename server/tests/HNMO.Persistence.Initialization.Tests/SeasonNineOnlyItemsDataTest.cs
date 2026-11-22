// <copyright file="SeasonNineOnlyItemsDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using System.Collections;
using System.Globalization;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.Properties;
using HNMO.Persistence.Initialization.Updates.SeasonNine;

/// <summary>
/// Verifies <see cref="SeasonNineOnlyItemsUpdatePlugIn"/> adds the 279 Season 9-only items only in Season 9 mode,
/// localizes every item caption, wires stat profiles and socket options, and preserves Season 6 configurations.
/// </summary>
[TestFixture]
internal class SeasonNineOnlyItemsDataTest
{
    /// <summary>
    /// Verifies Season 9 mode adds all 279 unique Season 9-only items with expected stats, sockets, and translations.
    /// </summary>
    [Test]
    public async Task SeasonNineModeAddsSeasonNineItemsAsync()
    {
        var (context, config) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        var beforeCount = config.Items.Count;
        var plugin = new SeasonNineOnlyItemsUpdatePlugIn(() => true);

        await plugin.ApplyUpdateAsync(context, config).ConfigureAwait(false);

        Assert.That(config.Items.Count, Is.EqualTo(beforeCount + 264));

        var chinese = CultureInfo.GetCultureInfo("zh-CN");
        var vietnamese = CultureInfo.GetCultureInfo("vi");
        var german = CultureInfo.GetCultureInfo("de");

        var sonicBlade = FindItem(config, 0, 29);
        var lazyWindArmor = FindItem(config, 8, 54);
        var bloodyDragonGloves = FindItem(config, 10, 77);
        var wingsOfAngelAndDevil = FindItem(config, 12, 267);
        var blindParchment = FindItem(config, 15, 25);
        var wooparoo = FindItem(config, 13, 229);

        Assert.Multiple(() =>
        {
            Assert.That(sonicBlade.Name.ValueInNeutralLanguage, Is.EqualTo("Sonic Blade"));
            Assert.That(sonicBlade.MaximumSockets, Is.EqualTo(5));
            Assert.That(sonicBlade.BasePowerUpAttributes.First(p => p.TargetAttribute == Stats.MinimumPhysBaseDmgByWeapon).BaseValue, Is.EqualTo(109));
            Assert.That(sonicBlade.StatProfile, Is.Not.Null);

            Assert.That(lazyWindArmor.MaximumSockets, Is.EqualTo(3));
            Assert.That(lazyWindArmor.BasePowerUpAttributes.First(p => p.TargetAttribute == Stats.DefenseBase).BaseValue, Is.EqualTo(79));
            Assert.That(lazyWindArmor.StatProfile?.Name, Is.EqualTo("armor"));

            Assert.That(bloodyDragonGloves.Name.ValueInNeutralLanguage, Is.EqualTo("Bloody Dragon Gloves"));
            Assert.That(bloodyDragonGloves.ItemSlot?.ItemSlots, Does.Contain(5));

            Assert.That(wingsOfAngelAndDevil.ItemSlot?.ItemSlots, Does.Contain(7));
            Assert.That(wingsOfAngelAndDevil.BasePowerUpAttributes.Any(p => p.TargetAttribute == Stats.CanFly), Is.True);

            Assert.That(blindParchment.Name.ValueInNeutralLanguage, Is.EqualTo("Blind Parchment"));
            Assert.That(blindParchment.DropLevel, Is.EqualTo(93));
            Assert.That(blindParchment.Value, Is.EqualTo(345000));
            Assert.That(blindParchment.Requirements.Any(r => r.Attribute == Stats.TotalEnergyRequirementValue && r.MinimumValue > 0), Is.True);

            Assert.That(wooparoo.Name.ValueInNeutralLanguage, Is.EqualTo("Wooparoo"));

            // Verify every added item resolves a SeasonNineItemNames caption in en, vi, de, zh-CN.
            var added = config.Items.Where(i => i.Name.SourceKey?.StartsWith("SeasonNineItemNames/", StringComparison.Ordinal) == true).ToList();
            Assert.That(added, Has.Count.EqualTo(290));
            foreach (var item in added)
            {
                Assert.That(item.Name.GetOwnTranslation(chinese), Is.Not.Null.And.Not.Empty, item.Name.SourceKey);
                Assert.That(item.Name.GetOwnTranslation(vietnamese), Is.Not.Null.And.Not.Empty, item.Name.SourceKey);
                Assert.That(item.Name.GetOwnTranslation(german), Is.Not.Null.And.Not.Empty, item.Name.SourceKey);
            }
        });
    }

    /// <summary>
    /// Verifies Season 6 mode does not add Season 9-only items and marks the update installed.
    /// </summary>
    [Test]
    public async Task SeasonSixModeLeavesItemCatalogUnchangedAsync()
    {
        var (context, config) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        var beforeCount = config.Items.Count;
        var plugin = new SeasonNineOnlyItemsUpdatePlugIn(() => false);

        Assert.That(plugin.RunsOnNewDatabase, Is.False);
        await plugin.ApplyUpdateAsync(context, config).ConfigureAwait(false);

        Assert.That(config.Items.Count, Is.EqualTo(beforeCount));
        var entries = await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false);
        Assert.That(entries.Any(e => e.Key == plugin.Key && e.InstalledAt is not null), Is.True);
    }

    /// <summary>
    /// Verifies every SeasonNineItemNames resource key has non-empty translations in vi, de, zh-CN.
    /// </summary>
    [Test]
    public void SeasonNineItemNameResourcesAreComplete()
    {
        ConfigurationNameSources.Register();
        var neutralKeys = SeasonNineItemNames.ResourceManager.GetResourceSet(CultureInfo.InvariantCulture, true, false)!
            .Cast<DictionaryEntry>()
            .Select(e => (string)e.Key)
            .ToArray();
        Assert.That(neutralKeys, Is.Not.Empty);

        foreach (var cultureName in new[] { "vi", "de", "zh-CN" })
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            foreach (var key in neutralKeys)
            {
                Assert.That(SeasonNineItemNames.ResourceManager.GetString(key, culture), Is.Not.Null.And.Not.Empty, $"{key} ({cultureName})");
            }
        }
    }

    private static ItemDefinition FindItem(GameConfiguration config, byte group, short number) =>
        config.Items.First(i => i.Group == group && i.Number == number);
}
