// <copyright file="CrywolfWarpLevelUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.PlugIns;

/// <summary>
/// This update sets the level of the Crywolf warp (move list entry 26) to 300, as in the move list of the Season 6
/// server suite (owner's choice, 21 Nov 2026; it was 190). A level the host changed is kept.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("D2A9E741-0B53-4F8C-86E1-5C3F7A90B24D")]
public class CrywolfWarpLevelUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Crywolf warp level of the Season 6 server suite";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "The Crywolf warp needs level 300 (was 190), unless the host changed it.";

    /// <summary>The index of the Crywolf warp.</summary>
    internal const int WarpIndex = 26;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 21, 16, 20, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.WarpList.FirstOrDefault(w => w.Index == WarpIndex) is { LevelRequirement: 190 } warp)
        {
            warp.LevelRequirement = 300;
        }

        return ValueTask.CompletedTask;
    }
}
