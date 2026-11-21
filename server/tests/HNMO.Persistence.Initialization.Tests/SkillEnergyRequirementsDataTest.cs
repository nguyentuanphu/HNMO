// <copyright file="SkillEnergyRequirementsDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.Skills;
using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.InMemory;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Verifies the energy requirements of Recovery (168), Power Slash (100), and the Fairy Elf summons (90-280)
/// match the Season 6 and Season 9 server suites.
/// </summary>
[TestFixture]
internal class SkillEnergyRequirementsDataTest
{
    /// <summary>
    /// A fresh Season 6 database requires 168 energy for Recovery, 100 energy for Power Slash,
    /// and 90/170/190/230/250/260/280 energy for the seven Fairy Elf summons.
    /// </summary>
    [Test]
    public async Task NewDatabaseHasSuiteEnergyRequirementsAsync()
    {
        var (context, config) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;

        Assert.Multiple(() =>
        {
            Assert.That(GetEnergyRequirement(config, SkillNumber.Recovery), Is.EqualTo(168));
            Assert.That(GetEnergyRequirement(config, SkillNumber.PowerSlash), Is.EqualTo(100));
            Assert.That(GetEnergyRequirement(config, SkillNumber.SummonGoblin), Is.EqualTo(90));
            Assert.That(GetEnergyRequirement(config, SkillNumber.SummonStoneGolem), Is.EqualTo(170));
            Assert.That(GetEnergyRequirement(config, SkillNumber.SummonAssassin), Is.EqualTo(190));
            Assert.That(GetEnergyRequirement(config, SkillNumber.SummonEliteYeti), Is.EqualTo(230));
            Assert.That(GetEnergyRequirement(config, SkillNumber.SummonDarkKnight), Is.EqualTo(250));
            Assert.That(GetEnergyRequirement(config, SkillNumber.SummonBali), Is.EqualTo(260));
            Assert.That(GetEnergyRequirement(config, SkillNumber.SummonSoldier), Is.EqualTo(280));
        });
    }

    /// <summary>
    /// The mandatory update plug-in migrates old HNMO values (Recovery 37, Power Slash omitted)
    /// while leaving host-customized energy requirements untouched.
    /// </summary>
    [Test]
    public async Task UpdateMigratesOldValuesAndPreservesHostChangesAsync()
    {
        var (context, config) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;

        var recovery = GetSkill(config, SkillNumber.Recovery);
        var powerSlash = GetSkill(config, SkillNumber.PowerSlash);
        FindRequirement(recovery, Stats.TotalEnergy)!.MinimumValue = 37;
        powerSlash.Requirements.Remove(FindRequirement(powerSlash, Stats.TotalEnergy)!);

        var update = new SkillEnergyRequirementsUpdatePlugIn();
        await update.ApplyUpdateAsync(context, config).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(update.IsMandatory, Is.True);
            Assert.That(GetEnergyRequirement(config, SkillNumber.Recovery), Is.EqualTo(168));
            Assert.That(GetEnergyRequirement(config, SkillNumber.PowerSlash), Is.EqualTo(100));
        });

        // Host customizes Recovery to 200 and Power Slash to 125; re-running keeps host changes.
        FindRequirement(recovery, Stats.TotalEnergy)!.MinimumValue = 200;
        FindRequirement(powerSlash, Stats.TotalEnergy)!.MinimumValue = 125;
        await update.ApplyUpdateAsync(context, config).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(GetEnergyRequirement(config, SkillNumber.Recovery), Is.EqualTo(200));
            Assert.That(GetEnergyRequirement(config, SkillNumber.PowerSlash), Is.EqualTo(125));
        });
    }

    private static Skill GetSkill(GameConfiguration config, SkillNumber number)
    {
        return config.Skills.Single(s => s.Number == (short)number);
    }

    private static AttributeRequirement? FindRequirement(Skill skill, AttributeDefinition stat)
    {
        return skill.Requirements.FirstOrDefault(r => r.Attribute == stat);
    }

    private static int GetEnergyRequirement(GameConfiguration config, SkillNumber number)
    {
        return FindRequirement(GetSkill(config, number), Stats.TotalEnergy)?.MinimumValue ?? 0;
    }

    private static async Task<(IContext Context, GameConfiguration GameConfiguration)> CreateSeason6ConfigurationAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        var init = new VersionSeasonSix.DataInitialization(provider, new NullLoggerFactory());
        await init.CreateInitialDataAsync(1, true).ConfigureAwait(false);

        var context = provider.CreateNewContext();
        var config = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
        return (context, config);
    }
}
