// <copyright file="AddCraftingBonusPercentageAttributePlugIn095D.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// This update adds the crafting bonus percentage attribute for version 0.95d.
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.AddCraftingBonusPercentageAttributePlugInBase_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.AddCraftingBonusPercentageAttributePlugInBase_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("9C6D1732-EA73-497C-9CD1-029ECEC644F4")]
public class AddCraftingBonusPercentageAttributePlugIn095D : AddCraftingBonusPercentageAttributePlugInBase
{
    /// <inheritdoc />
    public override string DataInitializationKey => Version095d.DataInitialization.Id;
}
