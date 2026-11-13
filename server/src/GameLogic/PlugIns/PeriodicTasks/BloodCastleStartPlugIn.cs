// <copyright file="BloodCastleStartPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.PeriodicTasks;

using System.Runtime.InteropServices;
using HNMO.GameLogic.MiniGames;
using HNMO.PlugIns;

/// <summary>
/// This plugin enables the start of the blood castle.
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.GameLogic.Properties.PlugInResources.BloodCastleStartPlugIn_Name), Description = nameof(HNMO.GameLogic.Properties.PlugInResources.BloodCastleStartPlugIn_Description), ResourceType = typeof(HNMO.GameLogic.Properties.PlugInResources))]
[Guid("95E68C14-AD87-4B3C-AF46-45B8F1C3BC2A")]
public sealed class BloodCastleStartPlugIn : MiniGameStartBasePlugIn<BloodCastleStartConfiguration, BloodCastleGameServerState>
{
    /// <inheritdoc />
    public override MiniGameType Key => MiniGameType.BloodCastle;

    /// <inheritdoc />
    public override object CreateDefaultConfig()
    {
        return BloodCastleStartConfiguration.Default;
    }

    /// <inheritdoc />
    protected override BloodCastleGameServerState CreateState(IGameContext gameContext)
    {
        return new BloodCastleGameServerState(gameContext);
    }
}