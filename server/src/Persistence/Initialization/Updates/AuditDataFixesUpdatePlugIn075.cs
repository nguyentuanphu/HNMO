// <copyright file="AuditDataFixesUpdatePlugIn075.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// The 25 Nov 2026 audit fix of Hanzo the Blacksmith's store for Version 0.75 databases.
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.AuditDataFixesUpdatePlugIn_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.AuditDataFixesUpdatePlugIn_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("9A2C4E61-3B5D-4F70-8E1A-2C6D9B0E4F37")]
public class AuditDataFixesUpdatePlugIn075 : AuditDataFixesUpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => HNMO.Persistence.Initialization.Properties.PlugInResources.AuditDataFixesUpdatePlugIn_Name;

    /// <inheritdoc />
    public override string Description => HNMO.Persistence.Initialization.Properties.PlugInResources.AuditDataFixesUpdatePlugIn_Description;

    /// <inheritdoc />
    public override string DataInitializationKey => Version075.DataInitialization.Id;
}
