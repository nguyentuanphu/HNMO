// <copyright file="SkillRangesAndNovaAgDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlayerActions.Skills;
using HNMO.Pathfinding;
using HNMO.Persistence.Initialization.Skills;
using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.InMemory;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Verifies area-skill distances (Evil Spirit 6, Cometfall 6, Hellfire 0, Inferno 0 with EffectRange 4)
/// and Nova / Chain Drive Strengthener AG consumptions match the Season 6/9 server suites.
/// </summary>
[TestFixture]
internal class SkillRangesAndNovaAgDataTest
{
    /// <summary>A fresh Season 6 database has the Season 6/9 server suites' skill distances and AG values, while keeping area hit radii.</summary>
    [Test]
    public async Task NewDatabaseHasSuiteSkillRangesAndNovaAgAsync()
    {
        var config = await CreateConfigurationAsync(new InMemoryPersistenceContextProvider()).ConfigureAwait(false);

        var evilSpirit = GetSkill(config, SkillNumber.EvilSpirit);
        var cometfall = GetSkill(config, SkillNumber.Cometfall);
        var hellfire = GetSkill(config, SkillNumber.Hellfire);
        var inferno = GetSkill(config, SkillNumber.Inferno);
        var nova = GetSkill(config, SkillNumber.Nova);
        var novaStart = GetSkill(config, SkillNumber.NovaStart);
        var chainDriveStr = GetSkill(config, SkillNumber.ChainDriveStrengthener);

        Assert.Multiple(() =>
        {
            Assert.That(evilSpirit.Range, Is.EqualTo(6));
            Assert.That(cometfall.Range, Is.EqualTo(6));
            Assert.That(hellfire.Range, Is.EqualTo(0));
            Assert.That(hellfire.AreaSkillSettings?.EffectRange, Is.EqualTo(2));
            Assert.That(inferno.Range, Is.EqualTo(0));
            Assert.That(inferno.AreaSkillSettings?.EffectRange, Is.EqualTo(4));
            Assert.That(GetAgCost(nova), Is.EqualTo(45));
            Assert.That(GetAgCost(novaStart), Is.EqualTo(0));
            Assert.That(GetAgCost(chainDriveStr), Is.EqualTo(20));

            var caster = new Point(100, 100);
            Assert.That(AreaSkillTargetSelector.GetQueryCenterAndRange(caster, caster, hellfire).Range, Is.EqualTo(2));
            Assert.That(AreaSkillTargetSelector.GetQueryCenterAndRange(caster, caster, inferno).Range, Is.EqualTo(4));
        });
    }

    /// <summary>The update plug-in migrates old HNMO values and leaves host-customized values untouched.</summary>
    [Test]
    public async Task UpdateMigratesOldValuesAndPreservesHostChangesAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        var config = await CreateConfigurationAsync(provider).ConfigureAwait(false);

        var evilSpirit = GetSkill(config, SkillNumber.EvilSpirit);
        var cometfall = GetSkill(config, SkillNumber.Cometfall);
        var hellfire = GetSkill(config, SkillNumber.Hellfire);
        var inferno = GetSkill(config, SkillNumber.Inferno);
        var nova = GetSkill(config, SkillNumber.Nova);
        var novaStart = GetSkill(config, SkillNumber.NovaStart);
        var chainDriveStr = GetSkill(config, SkillNumber.ChainDriveStrengthener);

        evilSpirit.Range = 7;
        cometfall.Range = 3;
        hellfire.Range = 4;
        inferno.Range = 4;
        inferno.AreaSkillSettings = null;
        SetAgCost(provider.CreateNewContext(), nova, 0);
        SetAgCost(provider.CreateNewContext(), novaStart, 45);
        SetAgCost(provider.CreateNewContext(), chainDriveStr, 22);

        var update = new SkillRangesAndNovaAgUpdatePlugIn();
        await update.ApplyUpdateAsync(provider.CreateNewContext(), config).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(update.IsMandatory, Is.True);
            Assert.That(evilSpirit.Range, Is.EqualTo(6));
            Assert.That(cometfall.Range, Is.EqualTo(6));
            Assert.That(hellfire.Range, Is.EqualTo(0));
            Assert.That(inferno.Range, Is.EqualTo(0));
            Assert.That(inferno.AreaSkillSettings?.EffectRange, Is.EqualTo(4));
            Assert.That(GetAgCost(nova), Is.EqualTo(45));
            Assert.That(GetAgCost(novaStart), Is.EqualTo(0));
            Assert.That(GetAgCost(chainDriveStr), Is.EqualTo(20));
        });

        // Host customizes Evil Spirit range to 8 and Nova AG to 30; re-running the update keeps them.
        evilSpirit.Range = 8;
        SetAgCost(provider.CreateNewContext(), nova, 30);
        await update.ApplyUpdateAsync(provider.CreateNewContext(), config).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(evilSpirit.Range, Is.EqualTo(8));
            Assert.That(GetAgCost(nova), Is.EqualTo(30));
        });
    }

    private static Skill GetSkill(GameConfiguration config, SkillNumber number) =>
        config.Skills.Single(s => s.Number == (short)number);

    private static int GetAgCost(Skill skill) =>
        skill.ConsumeRequirements.FirstOrDefault(r => r.Attribute == Stats.CurrentAbility)?.MinimumValue ?? 0;

    private static void SetAgCost(IContext context, Skill skill, int ag)
    {
        var req = skill.ConsumeRequirements.FirstOrDefault(r => r.Attribute == Stats.CurrentAbility);
        if (ag <= 0)
        {
            if (req is not null)
            {
                skill.ConsumeRequirements.Remove(req);
            }

            return;
        }

        if (req is null)
        {
            req = context.CreateNew<AttributeRequirement>();
            req.Attribute = Stats.CurrentAbility;
            skill.ConsumeRequirements.Add(req);
        }

        req.MinimumValue = ag;
    }

    private static async Task<GameConfiguration> CreateConfigurationAsync(InMemoryPersistenceContextProvider contextProvider)
    {
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using var context = contextProvider.CreateNewContext();
        return (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
    }
}
