// -----------------------------------------------------------------------
// <copyright file="SmallManaPotionConsumeHandler.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace HNMO.GameLogic.PlayerActions.ItemConsumeActions;

using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// Consume handler for small health potions.
/// </summary>
[Guid("A55849BC-7BD7-4444-A35A-F1AC1D48F179")]
[PlugIn]
[Display(Name = nameof(PlugInResources.SmallManaPotionConsumeHandler_Name), Description = nameof(PlugInResources.SmallManaPotionConsumeHandler_Description), ResourceType = typeof(PlugInResources))]
public class SmallManaPotionConsumeHandler : ManaPotionConsumeHandler
{
    /// <inheritdoc />
    public override ItemIdentifier Key => ItemConstants.SmallManaPotion;

    /// <inheritdoc/>
    protected override int Multiplier => 1;
}