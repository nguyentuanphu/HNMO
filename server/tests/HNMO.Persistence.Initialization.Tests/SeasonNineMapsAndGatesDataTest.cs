// <copyright file="SeasonNineMapsAndGatesDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using System.Collections;
using System.Globalization;
using HNMO.DataModel.Configuration;
using HNMO.Interfaces;
using HNMO.Persistence.Initialization.Properties;
using HNMO.Persistence.Initialization.Updates.SeasonNine;

/// <summary>
/// Verifies <see cref="SeasonNineMapsAndGatesUpdatePlugIn"/> adds the 10 Season 9 maps, embedded terrain data,
/// gates (417-461, 474-482), and Season 9 field spawns only in Season 9 mode, with full localization.
/// </summary>
[TestFixture]
internal class SeasonNineMapsAndGatesDataTest
{
    /// <summary>
    /// Verifies Season 9 mode adds the 10 maps with valid 65539-byte terrains, safezones, gates, and spawns.
    /// </summary>
    [Test]
    public async Task SeasonNineModeAddsSeasonNineMapsAndGatesAsync()
    {
        var (context, config) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        var beforeMapCount = config.Maps.Count;

        // Add synthetic Adniel (582) & Devil Fairy (586) to verify spawn wiring runs cleanly when monsters are present.
        var adniel = context.CreateNew<MonsterDefinition>();
        adniel.Number = 582;
        adniel.Designation = new LocalizedString("Element Master Adniel");
        adniel.ObjectKind = NpcObjectKind.PassiveNpc;
        config.Monsters.Add(adniel);

        var plugin = new SeasonNineMapsAndGatesUpdatePlugIn(() => true);
        await plugin.ApplyUpdateAsync(context, config).ConfigureAwait(false);

        Assert.That(config.Maps.Count, Is.EqualTo(beforeMapCount + 10));

        var chinese = CultureInfo.GetCultureInfo("zh-CN");
        var vietnamese = CultureInfo.GetCultureInfo("vi");
        var german = CultureInfo.GetCultureInfo("de");

        var acheron = config.Maps.First(m => m.Number == 91);
        var acheronWar = config.Maps.First(m => m.Number == 92);
        var debenter = config.Maps.First(m => m.Number == 95);
        var debenterWar = config.Maps.First(m => m.Number == 96);
        var ccSurvival = config.Maps.First(m => m.Number == 97);
        var itLeague1 = config.Maps.First(m => m.Number == 98);
        var itLeague2 = config.Maps.First(m => m.Number == 99);
        var uruk = config.Maps.First(m => m.Number == 100);
        var urukWar = config.Maps.First(m => m.Number == 101);
        var tormented = config.Maps.First(m => m.Number == 102);
        var dungeon = config.Maps.First(m => m.Number == 1);

        Assert.Multiple(() =>
        {
            Assert.That(acheron.Name.ValueInNeutralLanguage, Is.EqualTo("Acheron"));
            Assert.That(acheron.TerrainData, Has.Length.EqualTo(65539));
            Assert.That(acheron.SafezoneMap, Is.SameAs(acheron));
            Assert.That(acheron.ExitGates.Any(g => g.IsSpawnGate && g.X1 == 51 && g.Y1 == 190), Is.True);
            Assert.That(acheron.EnterGates.Any(g => g.Number == 435 && g.TargetGate?.Map == debenter), Is.True);
            Assert.That(acheron.EnterGates.Any(g => g.Number == 450 && g.TargetGate?.Map == uruk), Is.True);
            Assert.That(acheron.DropItemGroups, Is.Not.Empty);
            Assert.That(acheron.MonsterSpawns.Any(s => s.MonsterDefinition?.Number == 240), Is.True);
            Assert.That(acheron.MonsterSpawns.Any(s => s.MonsterDefinition?.Number == 582), Is.True);

            Assert.That(acheronWar.TerrainData, Is.EqualTo(acheron.TerrainData));
            Assert.That(acheronWar.SafezoneMap, Is.SameAs(acheronWar));
            Assert.That(debenter.TerrainData, Has.Length.EqualTo(65539));
            Assert.That(debenter.SafezoneMap, Is.SameAs(acheron));
            Assert.That(debenterWar.TerrainData, Has.Length.EqualTo(65539));
            Assert.That(ccSurvival.TerrainData, Has.Length.EqualTo(65539));
            Assert.That(itLeague1.TerrainData, Has.Length.EqualTo(65539));
            Assert.That(itLeague2.TerrainData, Has.Length.EqualTo(65539));
            Assert.That(uruk.TerrainData, Has.Length.EqualTo(65539));
            Assert.That(uruk.SafezoneMap, Is.SameAs(acheron));
            Assert.That(uruk.EnterGates.Any(g => g.Number == 452 && g.TargetGate?.Map == acheron), Is.True);
            Assert.That(uruk.EnterGates.Any(g => g.Number == 454 && g.TargetGate?.Map == uruk), Is.True);
            Assert.That(urukWar.TerrainData, Is.EqualTo(uruk.TerrainData));
            Assert.That(tormented.TerrainData, Has.Length.EqualTo(65539));

            Assert.That(dungeon.EnterGates.Any(g => g.Number == 475), Is.True);
            Assert.That(dungeon.EnterGates.Any(g => g.Number == 477), Is.True);
            Assert.That(dungeon.EnterGates.Any(g => g.Number == 479), Is.True);
            Assert.That(dungeon.EnterGates.Any(g => g.Number == 481), Is.True);

            var addedMaps = config.Maps.Where(m => m.Number >= 91).ToList();
            Assert.That(addedMaps, Has.Count.EqualTo(10));
            foreach (var map in addedMaps)
            {
                Assert.That(map.Name.GetOwnTranslation(chinese), Is.Not.Null.And.Not.Empty, map.Name.SourceKey);
                Assert.That(map.Name.GetOwnTranslation(vietnamese), Is.Not.Null.And.Not.Empty, map.Name.SourceKey);
                Assert.That(map.Name.GetOwnTranslation(german), Is.Not.Null.And.Not.Empty, map.Name.SourceKey);
            }
        });
    }

    /// <summary>
    /// Verifies Season 6 mode leaves maps and gates unchanged and records the update installed.
    /// </summary>
    [Test]
    public async Task SeasonSixModeLeavesMapsUnchangedAsync()
    {
        var (context, config) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        var beforeMapCount = config.Maps.Count;
        var dungeonGateCount = config.Maps.First(m => m.Number == 1).EnterGates.Count;
        var plugin = new SeasonNineMapsAndGatesUpdatePlugIn(() => false);

        Assert.That(plugin.RunsOnNewDatabase, Is.False);
        await plugin.ApplyUpdateAsync(context, config).ConfigureAwait(false);

        Assert.That(config.Maps.Count, Is.EqualTo(beforeMapCount));
        Assert.That(config.Maps.First(m => m.Number == 1).EnterGates.Count, Is.EqualTo(dungeonGateCount));
        var entries = await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false);
        Assert.That(entries.Any(e => e.Key == plugin.Key && e.InstalledAt is not null), Is.True);
    }

    /// <summary>
    /// Verifies every SeasonNineMapNames resource key has non-empty translations in vi, de, zh-CN.
    /// </summary>
    [Test]
    public void SeasonNineMapNameResourcesAreComplete()
    {
        ConfigurationNameSources.Register();
        var neutralKeys = SeasonNineMapNames.ResourceManager.GetResourceSet(CultureInfo.InvariantCulture, true, false)!
            .Cast<DictionaryEntry>()
            .Select(e => (string)e.Key)
            .ToArray();
        Assert.That(neutralKeys, Has.Length.EqualTo(10));

        foreach (var cultureName in new[] { "vi", "de", "zh-CN" })
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            foreach (var key in neutralKeys)
            {
                Assert.That(SeasonNineMapNames.ResourceManager.GetString(key, culture), Is.Not.Null.And.Not.Empty, $"{key} ({cultureName})");
            }
        }
    }
}
