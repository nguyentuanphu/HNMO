// <copyright file="HarmonyJewelConsumeHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.ItemConsumeActions;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration.Items;
using HNMO.PlugIns;

/// <summary>
/// The consume handler for the harmony jewel.
/// </summary>
[Guid("DAC3E5C2-FF0F-4773-AFBF-EBDC0C35336D")]
[PlugIn]
[Display(Name = nameof(PlugInResources.HarmonyJewelConsumeHandlerPlugIn_Name), Description = nameof(PlugInResources.HarmonyJewelConsumeHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
public class HarmonyJewelConsumeHandlerPlugIn : ItemUpgradeConsumeHandlerPlugIn
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HarmonyJewelConsumeHandlerPlugIn" /> class.
    /// </summary>
    public HarmonyJewelConsumeHandlerPlugIn()
        : base(new ItemUpgradeConfiguration(ItemOptionTypes.HarmonyOption, true, false, 0.6, ItemFailResult.None))
    {
    }

    /// <inheritdoc />
    public override ItemIdentifier Key => ItemConstants.JewelOfHarmony;

    /// <inheritdoc />
    protected override bool ItemCanHaveOption(Item item)
    {
        if (item.IsAncient())
        {
            // Until S16E2 ancient and socket items couldn't have harmony options: (the original game guide)
            return false;
        }

        return base.ItemCanHaveOption(item);
    }
}