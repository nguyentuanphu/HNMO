// <copyright file="AddGlobalMoneyAmountRateAttributePlugIn075.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// This update moves the MoneyAmountRate attribute to global base attributes for version 0.75.
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.AddGlobalMoneyAmountRateAttributePlugInBase_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.AddGlobalMoneyAmountRateAttributePlugInBase_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890")]
public class AddGlobalMoneyAmountRateAttributePlugIn075 : AddGlobalMoneyAmountRateAttributePlugInBase
{
    /// <inheritdoc />
    public override string DataInitializationKey => Version075.DataInitialization.Id;
}
