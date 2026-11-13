// <copyright file="GameMasterMagicEffectDefinition.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic;

using HNMO.DataModel.Attributes;

/// <summary>
/// A <see cref="MagicEffectDefinition"/> used to apply the GM mark
/// to a <see cref="Player"/> with <see cref="CharacterStatus.GameMaster"/> status.
/// </summary>
internal sealed class GameMasterMagicEffectDefinition : MagicEffectDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GameMasterMagicEffectDefinition"/> class
    /// with an empty power-up definitions list.
    /// </summary>
    public GameMasterMagicEffectDefinition()
    {
        this.PowerUpDefinitions = new List<PowerUpDefinition>(0);
    }
}
