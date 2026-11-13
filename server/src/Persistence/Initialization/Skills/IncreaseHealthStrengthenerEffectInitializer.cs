// <copyright file="IncreaseHealthStrengthenerEffectInitializer.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Skills;

using HNMO.DataModel.Configuration;
using HNMO.Interfaces;

/// <summary>
/// Initializer which initializes the increase health (stamina) strengthener effect.
/// </summary>
public class IncreaseHealthStrengthenerEffectInitializer : InitializerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IncreaseHealthStrengthenerEffectInitializer"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public IncreaseHealthStrengthenerEffectInitializer(IContext context, GameConfiguration gameConfiguration)
        : base(context, gameConfiguration)
    {
    }

    /// <inheritdoc/>
    public override void Initialize()
    {
        var magicEffect = this.Context.CreateNew<MagicEffectDefinition>();
        this.GameConfiguration.MagicEffects.Add(magicEffect);
        magicEffect.Number = (byte)MagicEffectNumber.IncreaseHealthStrengthener;
        magicEffect.Name = LocalizedString.FromResource(() => MagicEffectNames.IncreaseHealthStrengthenerSkillEffect);

        this.CopyMagicEffectValues(magicEffect, (short)MagicEffectNumber.IncreaseHealth);
    }
}