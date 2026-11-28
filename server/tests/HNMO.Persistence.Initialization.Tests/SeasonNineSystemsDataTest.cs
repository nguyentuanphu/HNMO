// <copyright file="SeasonNineSystemsDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using System.Globalization;
using System.Linq;
using HNMO.DataModel.Attributes;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.Initialization.Updates.SeasonNine;

/// <summary>
/// Verifies <see cref="SeasonNineSystemsUpdatePlugIn"/> updates Master Level cap to 330 and registers skills 618-621
/// only in Season 9 mode while preserving customized values and Season 6 defaults.
/// </summary>
[TestFixture]
internal class SeasonNineSystemsDataTest
{
    /// <summary>Verifies Season 9 raises Master Level cap to 330 and adds 4 localized skills (618-621).</summary>
    [Test]
    public async Task SeasonNineModeAppliesMasterCap330AndSkillsAsync()
    {
        var (context, config) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        var plugin = new SeasonNineSystemsUpdatePlugIn(() => true);

        await plugin.ApplyUpdateAsync(context, config).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(config.MaximumMasterLevel, Is.EqualTo(330));
            foreach (short num in new short[] { 618, 619, 620, 621 })
            {
                var sk = config.Skills.FirstOrDefault(s => s.Number == num);
                Assert.That(sk, Is.Not.Null, $"Skill {num}");
                Assert.That(sk!.MasterDefinition, Is.Null, $"Event/siege skill {num} must not be learned through the Master Tree");
                Assert.That(sk.Name.GetOwnTranslation(CultureInfo.GetCultureInfo("vi")), Is.Not.Null.And.Not.Empty);
                Assert.That(sk.Name.GetOwnTranslation(CultureInfo.GetCultureInfo("de")), Is.Not.Null.And.Not.Empty);
                Assert.That(sk.Name.GetOwnTranslation(CultureInfo.GetCultureInfo("zh-CN")), Is.Not.Null.And.Not.Empty);
                Assert.That(sk.QualifiedCharacters.Count, Is.GreaterThanOrEqualTo(6));
            }
        });
    }

    /// <summary>Verifies Season 6 marks update installed while preserving Master cap 200 and omitting 618-621.</summary>
    [Test]
    public async Task SeasonSixModePreserves200CapAndOmitsS9SkillsAsync()
    {
        var (context, config) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        var plugin = new SeasonNineSystemsUpdatePlugIn(() => false);

        await plugin.ApplyUpdateAsync(context, config).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(config.MaximumMasterLevel, Is.EqualTo(200));
            Assert.That(config.Skills.Any(s => s.Number is >= 618 and <= 621), Is.False);
        });
    }

    /// <summary>Verifies customized Master Level caps are preserved in Season 9 mode.</summary>
    [Test]
    public async Task CustomizedMasterCapIsPreservedAsync()
    {
        var (context, config) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        config.MaximumMasterLevel = 250;
        var plugin = new SeasonNineSystemsUpdatePlugIn(() => true);

        await plugin.ApplyUpdateAsync(context, config).ConfigureAwait(false);

        Assert.That(config.MaximumMasterLevel, Is.EqualTo(250));
    }

    /// <summary>
    /// No Rage Fighter line learns the four skills; all four Season 9 skills (618-621) cost 30 mana.
    /// Applying the update again changes nothing.
    /// </summary>
    [Test]
    public async Task ClassesAndManaCostsAreRightAndASecondRunChangesNothingAsync()
    {
        var (context, config) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        var plugin = new SeasonNineSystemsUpdatePlugIn(() => true);

        await plugin.ApplyUpdateAsync(context, config).ConfigureAwait(false);
        var first = Snapshot(config);
        await plugin.ApplyUpdateAsync(context, config).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            AssertSkills(config);
            Assert.That(Snapshot(config), Is.EqualTo(first));
        });
    }

    /// <summary>A database with version 1 or 2 (Rage Fighters qualified, or 618-620 costing 0 mana) is repaired by version 3.</summary>
    [Test]
    public async Task VersionOneAndTwoSkillsAreRepairedAsync()
    {
        var (context, config) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        var plugin = new SeasonNineSystemsUpdatePlugIn(() => true);
        await plugin.ApplyUpdateAsync(context, config).ConfigureAwait(false);

        // As version 2 left them (Rage Fighters removed, 618-620 costing 0 mana).
        var rageFighters = config.CharacterClasses.Where(c => c.Number is 24 or 25).ToList();
        var mana = Stats.CurrentMana.GetPersistent(config);
        foreach (var skill in config.Skills.Where(s => s.Number is >= 618 and <= 621))
        {
            rageFighters.ForEach(skill.QualifiedCharacters.Add);
            if (skill.Number != 621)
            {
                foreach (var req in skill.ConsumeRequirements.Where(r => Equals(r.Attribute, mana)).ToList())
                {
                    skill.ConsumeRequirements.Remove(req);
                }
            }
        }

        await plugin.ApplyUpdateAsync(context, config).ConfigureAwait(false);

        AssertSkills(config);
    }

    private static void AssertSkills(GameConfiguration config)
    {
        foreach (short number in new short[] { 618, 619, 620, 621 })
        {
            var skill = config.Skills.Single(s => s.Number == number);
            Assert.That(skill.QualifiedCharacters.Any(c => c.Number is 24 or 25), Is.False, $"{number}: Rage Fighter line");
            Assert.That(skill.QualifiedCharacters.Count(c => c.Number is 0 or 2 or 3), Is.EqualTo(3), $"{number}: wizard line");
            var manaCost = skill.ConsumeRequirements.Where(r => r.Attribute == Stats.CurrentMana).Sum(r => r.MinimumValue);
            Assert.That(manaCost, Is.EqualTo(30), $"{number}: mana");
        }
    }

    private static string Snapshot(GameConfiguration config)
        => string.Join(";", config.Skills.Where(s => s.Number is >= 618 and <= 621).OrderBy(s => s.Number)
            .Select(s => $"{s.Number}:{string.Join(",", s.QualifiedCharacters.Select(c => c.Number).OrderBy(n => n))}:{s.ConsumeRequirements.Count}"));
}
