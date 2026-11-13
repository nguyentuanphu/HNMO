// <copyright file="IAttacker.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic;

using HNMO.AttributeSystem;

/// <summary>
/// Interface for an object which can attack.
/// </summary>
public interface IAttacker : IIdentifiable, ILocateable
{
    /// <summary>
    /// Gets the attributes.
    /// </summary>
    IAttributeSystem Attributes { get; }

    /// <summary>
    /// Gets the state of the combo, if the attacker supports combos.
    /// </summary>
    ComboStateMachine? ComboState { get; }
}