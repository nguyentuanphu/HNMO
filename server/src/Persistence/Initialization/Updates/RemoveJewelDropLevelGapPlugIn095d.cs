// <copyright file="RemoveJewelDropLevelGapPlugIn095d.cs" company="HNMO">
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
[Guid("6614E91E-5749-478A-96A4-3240E7C1280E")]
public class RemoveJewelDropLevelGapPlugIn095D : RemoveJewelDropLevelGapPlugInBase
{
    /// <inheritdoc />
    public override string DataInitializationKey => Version095d.DataInitialization.Id;

    /// <inheritdoc />
    protected override async ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        await base.ApplyAsync(context, gameConfiguration).ConfigureAwait(false);
    }
}