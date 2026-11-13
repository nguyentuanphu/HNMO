// -----------------------------------------------------------------------
// <copyright file="ManaPotionConsumeHandler.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace HNMO.GameLogic.PlayerActions.ItemConsumeActions;

using HNMO.AttributeSystem;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.Views.Character;

/// <summary>
/// Consume handler for potions which refills the players attribute <see cref="Stats.CurrentMana"/>.
/// </summary>
public abstract class ManaPotionConsumeHandler : RecoverConsumeHandlerPlugIn.ManaHealthConsumeHandlerPlugIn, IItemConsumeHandlerPlugIn
{
    /// <inheritdoc/>
    protected override AttributeDefinition MaximumAttribute => Stats.MaximumMana;

    /// <inheritdoc/>
    protected override AttributeDefinition CurrentAttribute => Stats.CurrentMana;
}