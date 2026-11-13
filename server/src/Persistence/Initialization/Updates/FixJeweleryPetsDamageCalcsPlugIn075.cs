// <copyright file="FixJeweleryPetsDamageCalcsPlugIn075.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// This update fixes jewelery items and pet options related to damage.
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.FixJeweleryPetsDamageCalcsPlugInBase_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.FixJeweleryPetsDamageCalcsPlugInBase_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("46F50226-B0A2-4FE7-B708-AEB3F306A7C0")]
public class FixJeweleryPetsDamageCalcsPlugIn075 : FixJeweleryPetsDamageCalcsPlugInBase
{
    /// <inheritdoc />
    public override string DataInitializationKey => Version075.DataInitialization.Id;
}