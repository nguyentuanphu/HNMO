// <copyright file="AuditDataFixesUpdatePlugIn095d.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// The 25 Nov 2026 audit fix of Hanzo the Blacksmith's store for Version 0.95d databases.
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.AuditDataFixesUpdatePlugIn_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.AuditDataFixesUpdatePlugIn_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("B4D17E28-6C3A-4E95-A2F1-7D8C0B5E3A16")]
public class AuditDataFixesUpdatePlugIn095d : AuditDataFixesUpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => HNMO.Persistence.Initialization.Properties.PlugInResources.AuditDataFixesUpdatePlugIn_Name;

    /// <inheritdoc />
    public override string Description => HNMO.Persistence.Initialization.Properties.PlugInResources.AuditDataFixesUpdatePlugIn_Description;

    /// <inheritdoc />
    public override string DataInitializationKey => Version095d.DataInitialization.Id;
}
