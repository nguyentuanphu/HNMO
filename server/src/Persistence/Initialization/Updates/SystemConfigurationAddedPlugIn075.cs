// <copyright file="SystemConfigurationAddedPlugIn075.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.PlugIns;

/// <summary>
/// This updates adds the new <see cref="SystemConfiguration"/> with default settings
/// for Version 0.75.
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.SystemConfigurationAddedPlugInBase_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.SystemConfigurationAddedPlugInBase_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("F1151FDE-14F7-4945-AEE9-57DAB6449CFF")]
public class SystemConfigurationAddedPlugIn075 : SystemConfigurationAddedPlugInBase
{
    /// <inheritdoc />
    public override string DataInitializationKey => Version075.DataInitialization.Id;
}