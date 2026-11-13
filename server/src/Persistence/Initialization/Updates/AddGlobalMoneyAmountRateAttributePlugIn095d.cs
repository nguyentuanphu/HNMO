// <copyright file="AddGlobalMoneyAmountRateAttributePlugIn095d.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// This update moves the MoneyAmountRate attribute to global base attributes for version 0.95d.
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.AddGlobalMoneyAmountRateAttributePlugInBase_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.AddGlobalMoneyAmountRateAttributePlugInBase_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("B2C3D4E5-F6A7-8901-BCDE-F12345678901")]
public class AddGlobalMoneyAmountRateAttributePlugIn095d : AddGlobalMoneyAmountRateAttributePlugInBase
{
    /// <inheritdoc />
    public override string DataInitializationKey => Version095d.DataInitialization.Id;
}
