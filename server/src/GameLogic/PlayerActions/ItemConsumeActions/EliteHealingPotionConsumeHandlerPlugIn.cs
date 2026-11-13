// <copyright file="EliteHealingPotionConsumeHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.ItemConsumeActions;

using System.Runtime.InteropServices;
using HNMO.AttributeSystem;
using HNMO.GameLogic.Attributes;
using HNMO.PlugIns;

/// <summary>
/// Consume handler for the elite healing potion, which recovers the whole health immediately.
/// </summary>
[Guid("B880C615-8463-453A-B940-43629B241C53")]
[PlugIn]
[Display(Name = nameof(PlugInResources.EliteHealingPotionConsumeHandlerPlugIn_Name), Description = nameof(PlugInResources.EliteHealingPotionConsumeHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
public class EliteHealingPotionConsumeHandlerPlugIn : ElitePotionConsumeHandlerPlugIn
{
    /// <inheritdoc />
    public override ItemIdentifier Key => ItemConstants.EliteHealingPotion;

    /// <inheritdoc />
    protected override AttributeDefinition CurrentAttribute => Stats.CurrentHealth;

    /// <inheritdoc />
    protected override AttributeDefinition MaximumAttribute => Stats.MaximumHealth;

    /// <inheritdoc />
    protected override double RecoverPercentage => 100;
}
