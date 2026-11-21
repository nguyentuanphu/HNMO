// <copyright file="IncreaseBlockMasterFlatUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.Skills;
using HNMO.Persistence.Initialization.VersionSeasonSix;
using HNMO.PlugIns;

/// <summary>
/// Changes Rage Fighter's <see cref="SkillNumber.DefSuccessRateIncPowUp"/> master skill bonus from a percentage
/// of <see cref="Stats.DefenseRatePvm"/> (<see cref="AggregateType.AddRaw"/>) to a flat
/// <see cref="Stats.IncreaseBlockBonus"/> (<see cref="AggregateType.AddFinal"/>), matching the Season 6 server suite.
/// </summary>
[PlugIn]
[Display(
    Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.IncreaseBlockMasterFlatUpdatePlugIn_Name),
    Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.IncreaseBlockMasterFlatUpdatePlugIn_Description),
    ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("D524EC44-37DF-427B-93F4-443EA498E5B7")]
public class IncreaseBlockMasterFlatUpdatePlugIn : UpdatePlugInBase
{
    private const string OldPercentageFormula = $"{SkillsInitializer.Formula502} / 100";

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => "Make Increase Block Power Up flat";

    /// <inheritdoc />
    public override string Description => "Changes Rage Fighter's Increase Block Power Up master skill from a percentage of PvM defense rate to a flat defense rate bonus, matching the Season 6 server suite.";

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 22, 2, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.Skills.FirstOrDefault(s => s.Number == (short)SkillNumber.DefSuccessRateIncPowUp)?.MasterDefinition is { } masterDef
            && masterDef.ValueFormula == OldPercentageFormula
            && masterDef.Aggregation == AggregateType.AddRaw)
        {
            masterDef.ValueFormula = SkillsInitializer.Formula502;
            masterDef.Aggregation = AggregateType.AddFinal;
        }

        foreach (var effectNumber in new[] { MagicEffectNumber.IncreaseBlockPowerUp, MagicEffectNumber.IncreaseBlockMastery })
        {
            if (gameConfiguration.MagicEffects.FirstOrDefault(e => e.Number == (short)effectNumber) is not { } effect)
            {
                continue;
            }

            var percentagePlaceholder = effect.PowerUpDefinitions.FirstOrDefault(p =>
                p.TargetAttribute == Stats.IncreaseBlockBonus
                && p.Boost is { ConstantValue: { Value: 0f, AggregateType: AggregateType.AddRaw }, RelatedValues.Count: 0 });
            if (percentagePlaceholder is not null)
            {
                effect.PowerUpDefinitions.Remove(percentagePlaceholder);
            }
        }

        foreach (var characterClass in gameConfiguration.CharacterClasses)
        {
            var pvmScaling = characterClass.AttributeCombinations.FirstOrDefault(r =>
                r.TargetAttribute == Stats.IncreaseBlockBonus
                && r.InputAttribute == Stats.DefenseRatePvm
                && r.AggregateType == AggregateType.Multiplicate
                && r.InputOperand == 1f);
            if (pvmScaling is not null)
            {
                characterClass.AttributeCombinations.Remove(pvmScaling);
            }
        }

        return ValueTask.CompletedTask;
    }
}
