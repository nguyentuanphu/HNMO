// -----------------------------------------------------------------------
// <copyright file="SmallShieldPotionConsumeHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace HNMO.GameLogic.PlayerActions.ItemConsumeActions;

using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// Consume handler for small shield potions.
/// </summary>
[Guid("C403C8D7-9143-42BC-9894-CA285303E17A")]
[PlugIn]
[Display(Name = nameof(PlugInResources.SmallShieldPotionConsumeHandlerPlugIn_Name), Description = nameof(PlugInResources.SmallShieldPotionConsumeHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
public class SmallShieldPotionConsumeHandlerPlugIn : ShieldPotionConsumeHandlerPlugIn
{
    /// <inheritdoc />
    public override ItemIdentifier Key => ItemConstants.SmallShieldPotion;

    /// <inheritdoc />
    protected override double RecoverPercentage => 20;
}