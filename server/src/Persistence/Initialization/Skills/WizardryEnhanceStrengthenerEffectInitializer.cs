// <copyright file="WizardryEnhanceStrengthenerEffectInitializer.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Skills;

using HNMO.AttributeSystem;
using HNMO.DataModel.Attributes;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.Interfaces;

/// <summary>
/// Initializer which initializes the wizardry enhance strengthener effect.
/// </summary>
public class WizardryEnhanceStrengthenerEffectInitializer : InitializerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WizardryEnhanceStrengthenerEffectInitializer"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public WizardryEnhanceStrengthenerEffectInitializer(IContext context, GameConfiguration gameConfiguration)
        : base(context, gameConfiguration)
    {
    }

    /// <inheritdoc/>
    public override void Initialize()
    {
        var magicEffect = this.Context.CreateNew<MagicEffectDefinition>();
        this.GameConfiguration.MagicEffects.Add(magicEffect);
        magicEffect.Number = (byte)MagicEffectNumber.WizEnhanceStrengthener;
        magicEffect.Name = LocalizedString.FromResource(() => MagicEffectNames.WizardryEnhanceStrengthenerSkillEffect);

        this.CopyMagicEffectValues(magicEffect, (short)MagicEffectNumber.WizEnhance);

        var maxDmgPowerUp = this.Context.CreateNew<PowerUpDefinition>();
        magicEffect.PowerUpDefinitions.Add(maxDmgPowerUp);
        maxDmgPowerUp.TargetAttribute = Stats.MaximumWizBaseDmg.GetPersistent(this.GameConfiguration);
        maxDmgPowerUp.Boost = this.Context.CreateNew<PowerUpDefinitionValue>();
        maxDmgPowerUp.Boost.ConstantValue.Value = 1f;
        maxDmgPowerUp.Boost.ConstantValue.AggregateType = AggregateType.Multiplicate;
    }
}