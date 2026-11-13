// <copyright file="AddMovementSpeedAttributesPlugIn095D.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// Adds movement speed attributes to 0.95d game configurations.
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.AddMovementSpeedAttributesPlugInBase_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.AddMovementSpeedAttributesPlugInBase_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("7C38C30F-163B-4625-A82D-5C3A0A9ED883")]
public class AddMovementSpeedAttributesPlugIn095D : AddMovementSpeedAttributesPlugInBase
{
    /// <inheritdoc />
    public override string DataInitializationKey => Version095d.DataInitialization.Id;

    /// <inheritdoc />
    protected override int MaximumItemLevel => Version095d.Items.Constants.MaximumItemLevel;
}
