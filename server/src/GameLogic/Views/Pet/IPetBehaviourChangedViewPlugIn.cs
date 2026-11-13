// <copyright file="IPetBehaviourChangedViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views.Pet;

using HNMO.GameLogic.PlayerActions.Items;

/// <summary>
/// Interface of a view whose implementation informs about the behaviour change of the equipped pet.
/// </summary>
public interface IPetBehaviourChangedViewPlugIn : IViewPlugIn
{
    /// <summary>
    /// Informs about the behaviour change of the equipped pet.
    /// </summary>
    /// <param name="pet">The equipped pet.</param>
    /// <param name="behaviour">The behaviour.</param>
    /// <param name="target">The target.</param>
    ValueTask PetBehaviourChangedAsync(Item pet, PetBehaviour behaviour, IAttackable? target);
}