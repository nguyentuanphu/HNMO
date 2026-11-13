// <copyright file="DevilSquareStartPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.PeriodicTasks;

using System.Runtime.InteropServices;
using HNMO.GameLogic.MiniGames;
using HNMO.PlugIns;

/// <summary>
/// This plugin enables the start of the devil square.
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.GameLogic.Properties.PlugInResources.DevilSquareStartPlugIn_Name), Description = nameof(HNMO.GameLogic.Properties.PlugInResources.DevilSquareStartPlugIn_Description), ResourceType = typeof(HNMO.GameLogic.Properties.PlugInResources))]
[Guid("61C61A58-211E-4D6A-9EA1-D25E0C4A47C5")]
public sealed class DevilSquareStartPlugIn : MiniGameStartBasePlugIn<DevilSquareStartConfiguration, DevilSquareGameServerState>
{
    /// <summary>
    /// Gets the key under which the strategy is getting registered.
    /// </summary>
    public override MiniGameType Key => MiniGameType.DevilSquare;

    /// <inheritdoc />
    public override object CreateDefaultConfig()
    {
        return DevilSquareStartConfiguration.Default;
    }

    /// <inheritdoc />
    protected override DevilSquareGameServerState CreateState(IGameContext gameContext)
    {
        return new DevilSquareGameServerState(gameContext);
    }
}