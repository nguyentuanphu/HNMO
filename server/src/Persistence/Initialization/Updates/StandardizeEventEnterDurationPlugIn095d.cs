// <copyright file="StandardizeEventEnterDurationPlugIn095d.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// This update raises the entrance duration of all events with a shared entrance to the five minutes which every event entrance announces, for 0.95d databases.
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.StandardizeEventEnterDurationPlugInBase_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.StandardizeEventEnterDurationPlugInBase_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("A811B3E2-EAA1-434B-86E0-6D5DC23AEEF2")]
public class StandardizeEventEnterDurationPlugIn095d : StandardizeEventEnterDurationPlugInBase
{
    /// <inheritdoc />
    public override string DataInitializationKey => Version095d.DataInitialization.Id;
}
