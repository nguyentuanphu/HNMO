// <copyright file="RemoveJewelDropLevelGapPlugIn075.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.PlugIns;

/// <summary>
/// This update removes the existing drop level gap condition for jewels and similar items that should always drop.
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.RemoveJewelDropLevelGapPlugInBase_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.RemoveJewelDropLevelGapPlugInBase_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("CD958BC1-F17A-4C60-B66D-BD29D49B6ADA")]
public class RemoveJewelDropLevelGapPlugIn075 : RemoveJewelDropLevelGapPlugInBase
{
    /// <inheritdoc />
    public override string DataInitializationKey => Version075.DataInitialization.Id;

    /// <inheritdoc />
    protected override async ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        await base.ApplyAsync(context, gameConfiguration).ConfigureAwait(false);
    }
}