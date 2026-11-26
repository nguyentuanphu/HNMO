// <copyright file="SeasonNineMonstersDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using System.Collections;
using System.Globalization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.NPC;
using HNMO.Persistence.Initialization.Properties;
using HNMO.Persistence.Initialization.Updates.SeasonNine;

/// <summary>
/// Verifies <see cref="SeasonNineMonstersUpdatePlugIn"/> adds the 111 Season 9 monster and NPC definitions and
/// updates Dark Elf (Trainee Soldier) respawn delay only in Season 9 mode, with full localization.
/// </summary>
[TestFixture]
internal class SeasonNineMonstersDataTest
{
    /// <summary>
    /// Verifies Season 9 mode adds all 111 monsters/NPCs, updates Dark Elf respawn delay, and localizes all captions.
    /// </summary>
    [Test]
    public async Task SeasonNineModeAddsSeasonNineMonstersAndUpdatesDarkElfAsync()
    {
        var (context, config) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        var beforeCount = config.Monsters.Count;
        var plugin = new SeasonNineMonstersUpdatePlugIn(() => true);

        await plugin.ApplyUpdateAsync(context, config).ConfigureAwait(false);

        // Mining Area NPCs 605..607 are already ensured in Season 6 by S6EventsMonstersAndItemsUpdatePlugIn,
        // so SeasonNineMonstersUpdatePlugIn adds the remaining 108 of the 111 monsters/NPCs.
        Assert.That(config.Monsters.Count, Is.EqualTo(beforeCount + 108));

        var chinese = CultureInfo.GetCultureInfo("zh-CN");
        var vietnamese = CultureInfo.GetCultureInfo("vi");
        var german = CultureInfo.GetCultureInfo("de");

        var adniel = FindMonster(config, 582);
        var jin583 = FindMonster(config, 583);
        var devilFairy = FindMonster(config, 586);
        var debenterFairy = FindMonster(config, 608);
        var urukFairy = FindMonster(config, 670);
        var lordSilvester = FindMonster(config, 673);
        var monica = FindMonster(config, 682);
        var flameTrap = FindMonster(config, 689);
        var greenGoat = FindMonster(config, 726);
        var darkElf = FindMonster(config, 412);

        Assert.Multiple(() =>
        {
            Assert.That(adniel.ObjectKind, Is.EqualTo(NpcObjectKind.PassiveNpc));
            Assert.That(adniel.Designation.ValueInNeutralLanguage, Is.EqualTo("Element Master Adniel"));

            Assert.That(jin583.ObjectKind, Is.EqualTo(NpcObjectKind.PassiveNpc));
            Assert.That(jin583.NpcWindow, Is.EqualTo(NpcWindow.Merchant));

            Assert.That(devilFairy.ObjectKind, Is.EqualTo(NpcObjectKind.Monster));
            Assert.That(devilFairy[Stats.Level], Is.EqualTo(88));
            Assert.That(devilFairy[Stats.MaximumHealth], Is.EqualTo(29000));
            Assert.That(devilFairy[Stats.MinimumPhysBaseDmg], Is.EqualTo(408));
            Assert.That(devilFairy[Stats.MaximumPhysBaseDmg], Is.EqualTo(443));
            Assert.That(devilFairy[Stats.DefenseBase], Is.EqualTo(315));
            Assert.That(devilFairy[Stats.AttackRatePvm], Is.EqualTo(587));
            Assert.That(devilFairy[Stats.DefenseRatePvm], Is.EqualTo(195));

            Assert.That(debenterFairy[Stats.Level], Is.EqualTo(105));
            Assert.That(debenterFairy[Stats.MaximumHealth], Is.EqualTo(57000));

            Assert.That(urukFairy[Stats.Level], Is.EqualTo(115));
            Assert.That(urukFairy[Stats.MaximumHealth], Is.EqualTo(68000));
            Assert.That(urukFairy[Stats.MinimumPhysBaseDmg], Is.EqualTo(730));
            Assert.That(urukFairy[Stats.MaximumPhysBaseDmg], Is.EqualTo(760));
            Assert.That(urukFairy[Stats.DefenseBase], Is.EqualTo(630));

            Assert.That(lordSilvester[Stats.Level], Is.EqualTo(180));
            Assert.That(lordSilvester[Stats.MaximumHealth], Is.EqualTo(8000000));
            Assert.That(lordSilvester.RespawnDelay, Is.EqualTo(TimeSpan.FromSeconds(86400)));

            Assert.That(monica.ObjectKind, Is.EqualTo(NpcObjectKind.PassiveNpc));

            Assert.That(flameTrap.ObjectKind, Is.EqualTo(NpcObjectKind.Trap));
            Assert.That(flameTrap.IntelligenceTypeName, Is.EqualTo(typeof(AttackSingleWhenPressedTrapIntelligence).FullName));
            Assert.That(flameTrap.NumberOfMaximumItemDrops, Is.Zero);
            Assert.That(flameTrap[Stats.MinimumPhysBaseDmg], Is.EqualTo(5000));
            Assert.That(flameTrap[Stats.MaximumPhysBaseDmg], Is.EqualTo(6000));

            Assert.That(greenGoat.ObjectKind, Is.EqualTo(NpcObjectKind.PassiveNpc));
            Assert.That(darkElf.RespawnDelay, Is.EqualTo(TimeSpan.FromSeconds(10800)));

            var added = config.Monsters
                .Where(m => m.Designation.SourceKey?.StartsWith("SeasonNineMonsterNames/", StringComparison.Ordinal) == true)
                .ToList();
            Assert.That(added, Has.Count.EqualTo(108));
            foreach (var monster in added)
            {
                Assert.That(monster.Designation.GetOwnTranslation(chinese), Is.Not.Null.And.Not.Empty, monster.Designation.SourceKey);
                Assert.That(monster.Designation.GetOwnTranslation(vietnamese), Is.Not.Null.And.Not.Empty, monster.Designation.SourceKey);
                Assert.That(monster.Designation.GetOwnTranslation(german), Is.Not.Null.And.Not.Empty, monster.Designation.SourceKey);
            }
        });
    }

    /// <summary>
    /// Verifies Season 6 mode leaves monster definitions and Dark Elf respawn delay unchanged and marks update installed.
    /// </summary>
    [Test]
    public async Task SeasonSixModeLeavesMonstersUnchangedAsync()
    {
        var (context, config) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        var beforeCount = config.Monsters.Count;
        var darkElf = FindMonster(config, 412);
        var originalRespawn = darkElf.RespawnDelay;
        var plugin = new SeasonNineMonstersUpdatePlugIn(() => false);

        Assert.That(plugin.RunsOnNewDatabase, Is.False);
        await plugin.ApplyUpdateAsync(context, config).ConfigureAwait(false);

        Assert.That(config.Monsters.Count, Is.EqualTo(beforeCount));
        Assert.That(darkElf.RespawnDelay, Is.EqualTo(originalRespawn));
        var entries = await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false);
        Assert.That(entries.Any(e => e.Key == plugin.Key && e.InstalledAt is not null), Is.True);
    }

    /// <summary>
    /// Verifies every SeasonNineMonsterNames resource key has non-empty translations in vi, de, zh-CN.
    /// </summary>
    [Test]
    public void SeasonNineMonsterNameResourcesAreComplete()
    {
        ConfigurationNameSources.Register();
        var neutralKeys = SeasonNineMonsterNames.ResourceManager.GetResourceSet(CultureInfo.InvariantCulture, true, false)!
            .Cast<DictionaryEntry>()
            .Select(e => (string)e.Key)
            .ToArray();
        Assert.That(neutralKeys, Has.Length.EqualTo(111));

        foreach (var cultureName in new[] { "vi", "de", "zh-CN" })
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            foreach (var key in neutralKeys)
            {
                Assert.That(SeasonNineMonsterNames.ResourceManager.GetString(key, culture), Is.Not.Null.And.Not.Empty, $"{key} ({cultureName})");
            }
        }
    }

    private static MonsterDefinition FindMonster(GameConfiguration config, short number) =>
        config.Monsters.First(m => m.Number == number);
}
