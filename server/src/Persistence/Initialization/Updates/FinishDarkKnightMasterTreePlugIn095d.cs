// <copyright file="FinishDarkKnightMasterTreePlugIn095d.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.PlugIns;

/// <summary>
/// This update completes the dark knight master tree skills and effects. It also fixes the double wield damage calculations.
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.FinishDarkKnightMasterTreePlugInBase_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.FinishDarkKnightMasterTreePlugInBase_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("D4F7A9C2-1B3E-56D8-9F0A-7C2E4B1D5A8F")]
public class FinishDarkKnightMasterTreePlugIn095D : FinishDarkKnightMasterTreePlugInBase
{
    /// <inheritdoc />
    public override string DataInitializationKey => Version095d.DataInitialization.Id;
}
