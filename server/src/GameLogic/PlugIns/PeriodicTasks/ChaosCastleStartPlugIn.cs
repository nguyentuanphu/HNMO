// <copyright file="ChaosCastleStartPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.PeriodicTasks;

using System.Runtime.InteropServices;
using HNMO.GameLogic.MiniGames;
using HNMO.PlugIns;

/// <summary>
/// This plugin enables the start of the chaos castle.
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.GameLogic.Properties.PlugInResources.ChaosCastleStartPlugIn_Name), Description = nameof(HNMO.GameLogic.Properties.PlugInResources.ChaosCastleStartPlugIn_Description), ResourceType = typeof(HNMO.GameLogic.Properties.PlugInResources))]
[Guid("3AD96A70-ED24-4979-80B8-169E461E548F")]
public sealed class ChaosCastleStartPlugIn : MiniGameStartBasePlugIn<ChaosCastleStartConfiguration, ChaosCastleGameServerState>
{
    /// <inheritdoc />
    public override MiniGameType Key => MiniGameType.ChaosCastle;

    /// <inheritdoc />
    public override object CreateDefaultConfig()
    {
        return ChaosCastleStartConfiguration.Default;
    }

    /// <inheritdoc />
    protected override ChaosCastleGameServerState CreateState(IGameContext gameContext)
    {
        return new ChaosCastleGameServerState(gameContext);
    }
}