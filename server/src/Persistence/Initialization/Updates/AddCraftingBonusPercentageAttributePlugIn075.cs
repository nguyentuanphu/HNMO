// <copyright file="AddCraftingBonusPercentageAttributePlugIn075.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// This update adds the crafting bonus percentage attribute for version 0.75.
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.AddCraftingBonusPercentageAttributePlugInBase_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.AddCraftingBonusPercentageAttributePlugInBase_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("FCE91EF8-8195-47C1-894F-1C383001F2F3")]
public class AddCraftingBonusPercentageAttributePlugIn075 : AddCraftingBonusPercentageAttributePlugInBase
{
    /// <inheritdoc />
    public override string DataInitializationKey => Version075.DataInitialization.Id;
}
