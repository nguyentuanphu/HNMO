// <copyright file="MasterPassives314To324DataTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.Skills;
using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.InMemory;

/// <summary>
/// Verifies that Season 6 initialization and <see cref="MasterPassives314To324UpdatePlugIn"/> provide
/// all rank 6-9 passive master skills (<c>314..324</c> and class equivalents) with localized names and passive power-ups.
/// </summary>
[TestFixture]
internal class MasterPassives314To324DataTests
{
    private static readonly SkillNumber[] ExpectedPassiveSkills =
    [
        SkillNumber.CastInvincibility,
        SkillNumber.ArmorSetBonusInc,
        SkillNumber.Vengeance,
        SkillNumber.EnergyIncrease,
        SkillNumber.StaminaIncrease,
        SkillNumber.AgilityIncrease,
        SkillNumber.StrengthIncrease,
        SkillNumber.WingofStormAbsPowUp,
        SkillNumber.WingofStormDefPowUp,
        SkillNumber.WingofStormAttPowUp,
        SkillNumber.MaximumAgIncrease,
        SkillNumber.MaximumAttackPowerInc,
        SkillNumber.Inccritdamagerate,
        SkillNumber.RestoresallMana,
        SkillNumber.RestoresallHp,
        SkillNumber.Incexcdamagerate,
        SkillNumber.Incdoubledamagerate,
        SkillNumber.IncchanceofignoreDef,
        SkillNumber.RestoresallSd,
        SkillNumber.EternalWingsAbsPowUp,
        SkillNumber.EternalWingsDefPowUp,
        SkillNumber.EternalWingsAttPowUp,
        SkillNumber.MaximumWizardryInc,
        SkillNumber.IllusionWingsAbsPowUp,
        SkillNumber.IllusionWingsDefPowUp,
        SkillNumber.IllusionWingsAttPowUp,
        SkillNumber.MaximumAttPowerInc,
        SkillNumber.DimensionWingsDefPowUp,
        SkillNumber.DimensionWingsAttPowUp,
        SkillNumber.MaximumWizCurseInc,
        SkillNumber.WingofRuinDefPowUp,
        SkillNumber.WingofRuinAttPowUp,
        SkillNumber.EmperorCapeDefPowUp,
        SkillNumber.AddsCommandStat,
        SkillNumber.EmperorCapeAttPowUp,
        SkillNumber.ReigningCloakAbsPowUp,
        SkillNumber.ReigningCloakDefPowUp,
        SkillNumber.ReigningCloakAttPowUp,
        SkillNumber.CastInvincibilityFistMaster,
        SkillNumber.IncreaseSetDefenseFistMaster,
        SkillNumber.VengeanceFistMaster,
        SkillNumber.IncreaseEnergyFistMaster,
        SkillNumber.IncreaseHpFistMaster,
        SkillNumber.IncreaseAgilityFistMaster,
        SkillNumber.IncreaseStrengthFistMaster,
        SkillNumber.IncreaseMaximumAg,
        SkillNumber.IncreaseMaximumAttackPower,
        SkillNumber.IncreasesCritDamageChance,
        SkillNumber.RecoverManaFully,
        SkillNumber.RecoversHpFully,
        SkillNumber.IncreaseExcDamageChance,
        SkillNumber.IncreaseDoubleDamageChance,
        SkillNumber.IncreaseIgnoreDefChance,
        SkillNumber.RecoversSdFully,
    ];

    /// <summary>
    /// Fresh Season 6 data contains every expected rank 6-9 passive master skill with a passive power-up.
    /// </summary>
    [Test]
    public async Task FreshSeason6ContainsAllRankSixToNinePassivesAsync()
    {
        var (_, config) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        AssertPassivesPresent(config);
    }

    /// <summary>
    /// Applying <see cref="MasterPassives314To324UpdatePlugIn"/> to an older configuration missing those skills
    /// adds them cleanly and is idempotent when run twice.
    /// </summary>
    [Test]
    public async Task UpdatePlugInBackfillsAndIsIdempotentAsync()
    {
        var (context, config) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;

        var targetNumbers = ExpectedPassiveSkills.Select(s => (short)s).ToHashSet();
        var toRemove = config.Skills.Where(s => targetNumbers.Contains(s.Number)).ToList();
        foreach (var skill in toRemove)
        {
            config.Skills.Remove(skill);
        }

        foreach (var stat in new[]
        {
            Stats.MaxWizardryAndCurseDmgBonus,
            Stats.ArmorSetDefenseBonus,
            Stats.MasterWingDefenseBonus,
            Stats.MasterWingAttackBonus,
            Stats.MasterWingAbsorbBonus,
            Stats.CastInvincibilityChance,
            Stats.FullyRecoverShieldAfterHitChance,
        })
        {
            var existingAttr = config.Attributes.FirstOrDefault(a => a.Id == stat.Id);
            if (existingAttr is not null)
            {
                config.Attributes.Remove(existingAttr);
            }
        }

        var update = new MasterPassives314To324UpdatePlugIn();
        await update.ApplyUpdateAsync(context, config).ConfigureAwait(false);
        await update.ApplyUpdateAsync(context, config).ConfigureAwait(false);

        AssertPassivesPresent(config);
        Assert.That(update.DependsOn.Select(d => d.Key), Does.Contain(typeof(MasterTreeRanksAndLinksUpdatePlugIn).GUID));
    }

    private static void AssertPassivesPresent(GameConfiguration config)
    {
        var chinese = CultureInfo.GetCultureInfo("zh-CN");
        var vietnamese = CultureInfo.GetCultureInfo("vi");

        foreach (var skillNumber in ExpectedPassiveSkills)
        {
            var matches = config.Skills.Where(s => s.Number == (short)skillNumber).ToList();
            Assert.That(matches, Has.Count.EqualTo(1), $"Skill {skillNumber} ({(short)skillNumber})");

            var skill = matches[0];
            Assert.That(skill.SkillType, Is.EqualTo(SkillType.PassiveBoost), $"Skill {skillNumber}");
            Assert.That(skill.Name.SourceKey, Is.Not.Null.And.Not.Empty, $"Skill {skillNumber}");
            Assert.That(skill.Name.GetOwnTranslation(chinese), Is.Not.Null.And.Not.Empty, $"Skill {skillNumber} zh-CN");
            Assert.That(skill.Name.GetOwnTranslation(vietnamese), Is.Not.Null.And.Not.Empty, $"Skill {skillNumber} vi");
            Assert.That(skill.MasterDefinition, Is.Not.Null, $"Skill {skillNumber}");
            Assert.That(skill.MasterDefinition!.Rank, Is.GreaterThanOrEqualTo(6), $"Skill {skillNumber}");
            Assert.That(skill.MasterDefinition.PassivePowerUps, Has.Count.EqualTo(1), $"Skill {skillNumber}");
        }
    }

    private static async Task<(IContext Context, GameConfiguration Config)> CreateSeason6ConfigurationAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        var initializer = new VersionSeasonSix.DataInitialization(provider, NullLoggerFactory.Instance);
        await initializer.CreateInitialDataAsync(1, false).ConfigureAwait(false);
        var context = provider.CreateNewContext();
        var config = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        return (context, config);
    }
}
