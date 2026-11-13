// <copyright file="FixWingsDmgRatesUpdatePlugInSeason6.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.AttributeSystem;
using HNMO.PlugIns;

/// <summary>
/// This update fixes the wings damage absorption and increase bonus level tables values for a <see cref="CombinedElement"/> (sum) calculation, instead of a compound calculation
/// for season 6.
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.FixWingsDmgRatesUpdatePlugInBase_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.FixWingsDmgRatesUpdatePlugInBase_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("03F49890-CB0E-40B7-A590-174BBA1962F4")]
public class FixWingsDmgRatesUpdatePlugInSeason6 : FixWingsDmgRatesUpdatePlugInBase
{
    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;
}