// <copyright file="LifeSwellProficiencyEffectInitializer.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Skills;

using HNMO.AttributeSystem;
using HNMO.DataModel.Attributes;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.Interfaces;

/// <summary>
/// Initializer which initializes the life swell proficiency effect.
/// </summary>
public class LifeSwellProficiencyEffectInitializer : InitializerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LifeSwellProficiencyEffectInitializer"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public LifeSwellProficiencyEffectInitializer(IContext context, GameConfiguration gameConfiguration)
        : base(context, gameConfiguration)
    {
    }

    /// <inheritdoc/>
    public override void Initialize()
    {
        var magicEffect = this.Context.CreateNew<MagicEffectDefinition>();
        this.GameConfiguration.MagicEffects.Add(magicEffect);
        magicEffect.Number = (byte)MagicEffectNumber.GreaterFortitudeProficiency;
        magicEffect.Name = LocalizedString.FromResource(() => MagicEffectNames.LifeSwellProficiencySkillEffect);

        this.CopyMagicEffectValues(magicEffect, (short)MagicEffectNumber.GreaterFortitude);

        // one percent per party member in view
        var boostPerPartyMember = this.Context.CreateNew<AttributeRelationship>();
        boostPerPartyMember.InputAttribute = Stats.NearbyPartyMemberCount.GetPersistent(this.GameConfiguration);
        boostPerPartyMember.InputOperator = InputOperator.Multiply;
        boostPerPartyMember.InputOperand = 1f / 100;

        var manaPowerUpDefinition = this.Context.CreateNew<PowerUpDefinition>();
        magicEffect.PowerUpDefinitions.Add(manaPowerUpDefinition);
        manaPowerUpDefinition.TargetAttribute = Stats.SwellLifeManaIncrease.GetPersistent(this.GameConfiguration);
        manaPowerUpDefinition.Boost = this.Context.CreateNew<PowerUpDefinitionValue>();
        manaPowerUpDefinition.Boost.RelatedValues.Add(boostPerPartyMember);
    }
}