// -----------------------------------------------------------------------
// <copyright file="LargeHealthPotionConsumeHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace HNMO.GameLogic.PlayerActions.ItemConsumeActions;

using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// Consume handler for big health potions.
/// </summary>
[Guid("5035BBF5-A45D-454F-9D15-4DB6F725DCFB")]
[PlugIn]
[Display(Name = nameof(PlugInResources.LargeHealthPotionConsumeHandlerPlugIn_Name), Description = nameof(PlugInResources.LargeHealthPotionConsumeHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
public class LargeHealthPotionConsumeHandlerPlugIn : HealthPotionConsumeHandlerPlugIn
{
    /// <inheritdoc />
    public override ItemIdentifier Key => ItemConstants.LargeHealingPotion;

    /// <inheritdoc/>
    protected override int Multiplier => 3;
}