// <copyright file="AddCrystalSwordIceChanceUpdatePlugInSeasonSix.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// Adds the Crystal Sword ice chance to existing Season 6 configurations.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.AddCrystalSwordIceChanceUpdatePlugIn_Name), Description = nameof(PlugInResources.AddCrystalSwordIceChanceUpdatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("85AD7C42-7BAE-42A0-8CB1-0F3CC894DF05")]
public class AddCrystalSwordIceChanceUpdatePlugInSeasonSix : AddCrystalSwordIceChanceUpdatePlugInBase
{
    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;
}
