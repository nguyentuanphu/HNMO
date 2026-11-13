// <copyright file="ConfigureNpcTalkPlugInsPlugIn075.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// This update configures the NPCs of the plugins which handle talking to an NPC.
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.ConfigureNpcTalkPlugInsPlugIn_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.ConfigureNpcTalkPlugInsPlugIn_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("E6A089FE-5A7E-4D11-92CB-7BEA5DA267A1")]
public class ConfigureNpcTalkPlugInsPlugIn075 : ConfigureNpcTalkPlugInsPlugInBase
{
    /// <inheritdoc />
    public override string DataInitializationKey => Version075.DataInitialization.Id;
}
