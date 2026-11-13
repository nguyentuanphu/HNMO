// <copyright file="SystemConfigurationAddedPlugIn095d.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.PlugIns;

/// <summary>
/// This updates adds the new <see cref="SystemConfiguration"/> with default settings
/// for version 0.95d.
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.SystemConfigurationAddedPlugInBase_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.SystemConfigurationAddedPlugInBase_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("EC9FE71E-5C6C-456A-AC75-428EBA3FF626")]
public class SystemConfigurationAddedPlugIn095d : SystemConfigurationAddedPlugInBase
{
    /// <inheritdoc />
    public override string DataInitializationKey => Version095d.DataInitialization.Id;
}