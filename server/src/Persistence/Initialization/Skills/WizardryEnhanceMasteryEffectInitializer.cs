// <copyright file="WizardryEnhanceMasteryEffectInitializer.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Skills;

using HNMO.DataModel.Attributes;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.Interfaces;

/// <summary>
/// Initializer which initializes the wizardry enhance mastery effect.
/// </summary>
public class WizardryEnhanceMasteryEffectInitializer : InitializerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WizardryEnhanceMasteryEffectInitializer"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public WizardryEnhanceMasteryEffectInitializer(IContext context, GameConfiguration gameConfiguration)
        : base(context, gameConfiguration)
    {
    }

    /// <inheritdoc/>
    public override void Initialize()
    {
        var magicEffect = this.Context.CreateNew<MagicEffectDefinition>();
        this.GameConfiguration.MagicEffects.Add(magicEffect);
        magicEffect.Number = (byte)MagicEffectNumber.WizEnhanceMastery;
        magicEffect.Name = LocalizedString.FromResource(() => MagicEffectNames.WizardryEnhanceMasterySkillEffect);

        this.CopyMagicEffectValues(magicEffect, (short)MagicEffectNumber.WizEnhanceStrengthener);

        var critChancePowerUp = this.Context.CreateNew<PowerUpDefinition>();
        magicEffect.PowerUpDefinitions.Add(critChancePowerUp);
        critChancePowerUp.TargetAttribute = Stats.CriticalDamageChance.GetPersistent(this.GameConfiguration);
        critChancePowerUp.Boost = this.Context.CreateNew<PowerUpDefinitionValue>();
    }
}