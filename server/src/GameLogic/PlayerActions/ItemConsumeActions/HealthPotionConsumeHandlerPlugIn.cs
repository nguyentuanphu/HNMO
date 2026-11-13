// -----------------------------------------------------------------------
// <copyright file="HealthPotionConsumeHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace HNMO.GameLogic.PlayerActions.ItemConsumeActions;

using HNMO.AttributeSystem;
using HNMO.GameLogic.Attributes;

/// <summary>
/// The consume handler for a potion that recovers health.
/// </summary>
public abstract class HealthPotionConsumeHandlerPlugIn : RecoverConsumeHandlerPlugIn.ManaHealthConsumeHandlerPlugIn, IItemConsumeHandlerPlugIn
{
    /// <inheritdoc/>
    protected override AttributeDefinition MaximumAttribute => Stats.MaximumHealth;

    /// <inheritdoc/>
    protected override AttributeDefinition CurrentAttribute => Stats.CurrentHealth;
}