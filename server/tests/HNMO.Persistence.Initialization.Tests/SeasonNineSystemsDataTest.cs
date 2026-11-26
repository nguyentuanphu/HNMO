// <copyright file="SeasonNineSystemsDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using System.Globalization;
using System.Linq;
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
                Assert.That(sk!.Name.GetOwnTranslation(CultureInfo.GetCultureInfo("vi")), Is.Not.Null.And.Not.Empty);
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
}
