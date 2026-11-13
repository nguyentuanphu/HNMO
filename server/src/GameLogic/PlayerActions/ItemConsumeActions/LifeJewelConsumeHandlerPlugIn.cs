// -----------------------------------------------------------------------
// <copyright file="LifeJewelConsumeHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace HNMO.GameLogic.PlayerActions.ItemConsumeActions;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration.Items;
using HNMO.PlugIns;

/// <summary>
/// Consume handler for the Jewel of Life which adds and increases the item option of <see cref="ItemOptionTypes.Option"/>.
/// </summary>
/// <remarks>
/// The success chance and the behaviour on a failed upgrade are configurable, see
/// <see cref="ItemUpgradeConsumeHandlerPlugIn.ItemUpgradeConfiguration"/>.
/// </remarks>
[Guid("8AC6592D-D51C-47C9-B491-4778C615691D")]
[PlugIn]
[Display(Name = nameof(PlugInResources.LifeJewelConsumeHandlerPlugIn_Name), Description = nameof(PlugInResources.LifeJewelConsumeHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
public class LifeJewelConsumeHandlerPlugIn : ItemUpgradeConsumeHandlerPlugIn
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LifeJewelConsumeHandlerPlugIn" /> class.
    /// </summary>
    public LifeJewelConsumeHandlerPlugIn()
        : base(new ItemUpgradeConfiguration(ItemOptionTypes.Option, true, true, 0.5, ItemFailResult.RemoveOption))
    {
    }

    /// <inheritdoc />
    public override ItemIdentifier Key => ItemConstants.JewelOfLife;
}
