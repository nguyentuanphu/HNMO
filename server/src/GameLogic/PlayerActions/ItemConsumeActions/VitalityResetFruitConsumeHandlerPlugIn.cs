// <copyright file="VitalityResetFruitConsumeHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.ItemConsumeActions;

using System.Runtime.InteropServices;
using HNMO.AttributeSystem;
using HNMO.GameLogic.Attributes;
using HNMO.PlugIns;

/// <summary>
/// Consume handler for the reset fruit of vitality.
/// </summary>
[Guid("E1A64DC8-846C-481F-86C3-DFE5949DAA18")]
[PlugIn]
[Display(Name = nameof(PlugInResources.VitalityResetFruitConsumeHandlerPlugIn_Name), Description = nameof(PlugInResources.VitalityResetFruitConsumeHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
public class VitalityResetFruitConsumeHandlerPlugIn : ResetFruitConsumeHandlerPlugIn
{
    /// <inheritdoc />
    public override ItemIdentifier Key => ItemConstants.VitalityResetFruit;

    /// <inheritdoc />
    protected override AttributeDefinition StatAttribute => Stats.BaseVitality;
}
