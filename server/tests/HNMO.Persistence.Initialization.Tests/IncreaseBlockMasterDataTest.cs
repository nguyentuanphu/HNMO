// <copyright file="IncreaseBlockMasterDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.AttributeSystem;
using HNMO.DataModel.Attributes;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.Skills;
using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.Initialization.VersionSeasonSix;
using HNMO.Persistence.InMemory;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Verifies Rage Fighter's Increase Block Power Up (<c>569</c>) uses a flat bonus (<c>AddFinal</c>, <c>Formula502</c>)
/// rather than a percentage of <see cref="Stats.DefenseRatePvm"/> (<c>AddRaw</c>, <c>Formula502 / 100</c>),
/// matching the Season 6 server suite.
/// </summary>
[TestFixture]
internal class IncreaseBlockMasterDataTest
{
    /// <summary>A fresh Season 6 database configures DefSuccessRateIncPowUp as a flat IncreaseBlockBonus.</summary>
    [Test]
    public async Task NewDatabaseHasFlatIncreaseBlockMasterDefinitionAsync()
    {
        var (context, config) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;

        var skill = config.Skills.Single(s => s.Number == (short)SkillNumber.DefSuccessRateIncPowUp);
        var powerUpEffect = config.MagicEffects.Single(e => e.Number == (short)MagicEffectNumber.IncreaseBlockPowerUp);
        var masteryEffect = config.MagicEffects.Single(e => e.Number == (short)MagicEffectNumber.IncreaseBlockMastery);

        Assert.Multiple(() =>
        {
            Assert.That(skill.MasterDefinition!.ValueFormula, Is.EqualTo(SkillsInitializer.Formula502));
            Assert.That(skill.MasterDefinition.Aggregation, Is.EqualTo(AggregateType.AddFinal));
            Assert.That(powerUpEffect.PowerUpDefinitions.Count(p => p.TargetAttribute == Stats.IncreaseBlockBonus), Is.EqualTo(1));
            Assert.That(masteryEffect.PowerUpDefinitions.Count(p => p.TargetAttribute == Stats.IncreaseBlockBonus), Is.EqualTo(1));
            Assert.That(
                config.CharacterClasses.SelectMany(c => c.AttributeCombinations).Any(r =>
                    r.TargetAttribute == Stats.IncreaseBlockBonus && r.InputAttribute == Stats.DefenseRatePvm),
                Is.False);
        });
    }

    /// <summary>The mandatory update migrates old percentage-based Increase Block master data and preserves host edits.</summary>
    [Test]
    public async Task UpdateMigratesOldPercentageMasterToFlatAndPreservesHostChangesAsync()
    {
        var (context, config) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;

        var skill = config.Skills.Single(s => s.Number == (short)SkillNumber.DefSuccessRateIncPowUp);
        skill.MasterDefinition!.ValueFormula = $"{SkillsInitializer.Formula502} / 100";
        skill.MasterDefinition.Aggregation = AggregateType.AddRaw;

        var powerUpEffect = config.MagicEffects.Single(e => e.Number == (short)MagicEffectNumber.IncreaseBlockPowerUp);
        var placeholder = context.CreateNew<PowerUpDefinition>();
        placeholder.TargetAttribute = Stats.IncreaseBlockBonus.GetPersistent(config);
        placeholder.Boost = context.CreateNew<PowerUpDefinitionValue>();
        placeholder.Boost.ConstantValue.Value = 0f;
        placeholder.Boost.ConstantValue.AggregateType = AggregateType.AddRaw;
        powerUpEffect.PowerUpDefinitions.Add(placeholder);

        var rfClass = config.CharacterClasses.First();
        var rel = context.CreateNew<AttributeRelationship>();
        rel.TargetAttribute = Stats.IncreaseBlockBonus.GetPersistent(config);
        rel.InputAttribute = Stats.DefenseRatePvm.GetPersistent(config);
        rel.InputOperator = InputOperator.Multiply;
        rel.InputOperand = 1f;
        rel.AggregateType = AggregateType.Multiplicate;
        rfClass.AttributeCombinations.Add(rel);

        var update = new IncreaseBlockMasterFlatUpdatePlugIn();
        await update.ApplyUpdateAsync(context, config).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(update.IsMandatory, Is.True);
            Assert.That(skill.MasterDefinition.ValueFormula, Is.EqualTo(SkillsInitializer.Formula502));
            Assert.That(skill.MasterDefinition.Aggregation, Is.EqualTo(AggregateType.AddFinal));
            Assert.That(powerUpEffect.PowerUpDefinitions.Count(p => p.TargetAttribute == Stats.IncreaseBlockBonus), Is.EqualTo(1));
            Assert.That(rfClass.AttributeCombinations.Contains(rel), Is.False);
        });

        // Host sets custom flat formula; re-running keeps host edit.
        skill.MasterDefinition.ValueFormula = "level * 2";
        await update.ApplyUpdateAsync(context, config).ConfigureAwait(false);
        Assert.That(skill.MasterDefinition.ValueFormula, Is.EqualTo("level * 2"));
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
