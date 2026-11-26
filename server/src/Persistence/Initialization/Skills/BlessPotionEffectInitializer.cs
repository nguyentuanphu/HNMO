// <copyright file="BlessPotionEffectInitializer.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Skills;

using HNMO.AttributeSystem;
using HNMO.DataModel.Attributes;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.Interfaces;

/// <summary>
/// Initializer which initializes the bless potion effect.
/// </summary>
public class BlessPotionEffectInitializer : InitializerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BlessPotionEffectInitializer"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public BlessPotionEffectInitializer(IContext context, GameConfiguration gameConfiguration)
        : base(context, gameConfiguration)
    {
    }

    /// <inheritdoc/>
    public override void Initialize()
    {
        var magicEffect = this.Context.CreateNew<MagicEffectDefinition>();
        this.GameConfiguration.MagicEffects.Add(magicEffect);
        magicEffect.Number = (byte)MagicEffectNumber.PotionOfBless;
        magicEffect.SubType = 255 - (byte)MagicEffectNumber.PotionOfBless;
        magicEffect.Name = LocalizedString.FromResource(() => MagicEffectNames.PotionOfBlessEffect);
        magicEffect.InformObservers = false;
        magicEffect.SendDuration = false;
        magicEffect.StopByDeath = true;
        magicEffect.Duration = this.Context.CreateNew<PowerUpDefinitionValue>();
        magicEffect.Duration.ConstantValue.Value = 120;

        // The damage bonus against castle gates and statues is applied by the damage calculation (AttackableExtensions),
        // not by a power-up, so the potion no longer raises the damage against everything else.
    }
}