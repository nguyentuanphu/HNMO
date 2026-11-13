// <copyright file="MoveServerPortsOutOfDynamicRangePlugIn075.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// This update moves the ports of the game servers and the chat server out of the dynamic port range
/// for Version 0.75.
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.MoveServerPortsOutOfDynamicRangePlugInBase_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.MoveServerPortsOutOfDynamicRangePlugInBase_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("4D334E06-B646-46A2-B4B4-B0C303D49FA6")]
public class MoveServerPortsOutOfDynamicRangePlugIn075 : MoveServerPortsOutOfDynamicRangePlugInBase
{
    /// <inheritdoc />
    public override string DataInitializationKey => Version075.DataInitialization.Id;
}
