// <copyright file="FixOrbRequirementsPlugIn075.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// Fixes the energy requirements of the orbs of version 0.75.
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.FixOrbRequirementsPlugInBase_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.FixOrbRequirementsPlugInBase_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("3E2F21ED-3BA7-4EB5-B268-815D40BB3086")]
public class FixOrbRequirementsPlugIn075 : FixOrbRequirementsPlugInBase
{
    /// <inheritdoc />
    public override string DataInitializationKey => Version075.DataInitialization.Id;

    /// <inheritdoc />
    private protected override IReadOnlyList<(byte Group, byte Number, int Energy, int Strength, int Agility)> Corrections => FairyElfOrbs;
}
