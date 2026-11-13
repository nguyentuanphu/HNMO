// <copyright file="SystemConfigurationAddedPlugInSeason6.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.PlugIns;

/// <summary>
/// This updates adds the new <see cref="SystemConfiguration"/> with default settings
/// for season 6.
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.SystemConfigurationAddedPlugInBase_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.SystemConfigurationAddedPlugInBase_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("7231172F-51AD-4129-9003-C1ACC7E04147")]
public class SystemConfigurationAddedPlugInSeason6 : SystemConfigurationAddedPlugInBase
{
    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;
}