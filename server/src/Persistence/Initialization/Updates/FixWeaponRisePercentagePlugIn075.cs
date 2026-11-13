// <copyright file="FixWeaponRisePercentagePlugIn075.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// This update fixes weapons (staff) rise percentage.
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.FixWeaponRisePercentagePlugInBase_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.FixWeaponRisePercentagePlugInBase_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("5B63534D-E5DF-46B1-992D-C1637B197EE1")]
public class FixWeaponRisePercentagePlugIn075 : FixWeaponRisePercentagePlugInBase
{
    /// <inheritdoc />
    public override string DataInitializationKey => Version075.DataInitialization.Id;
}