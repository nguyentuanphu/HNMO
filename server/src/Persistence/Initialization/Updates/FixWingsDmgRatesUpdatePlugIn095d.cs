// <copyright file="FixWingsDmgRatesUpdatePlugIn095d.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.AttributeSystem;
using HNMO.PlugIns;

/// <summary>
/// This update fixes the wings damage absorption and increase bonus level tables values for a <see cref="CombinedElement"/> (sum) calculation, instead of a compound calculation
/// for version 095d.
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.FixWingsDmgRatesUpdatePlugInBase_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.FixWingsDmgRatesUpdatePlugInBase_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("F45FA4D0-B19B-48E2-9592-A37F3B36348A")]
public class FixWingsDmgRatesUpdatePlugIn095D : FixWingsDmgRatesUpdatePlugInBase
{
    /// <inheritdoc />
    public override string DataInitializationKey => Version095d.DataInitialization.Id;
}