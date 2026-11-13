// <copyright file="CastleSiegeMachineRegionNotifyPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.CastleSiege;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views.CastleSiege;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Pathfinding;
using HNMO.PlugIns;
using MachinePacketType = HNMO.Network.Packets.ServerToClient.CastleSiegeMachineType;
using MachineType = HNMO.GameLogic.CastleSiege.CastleSiegeMachineType;

/// <summary>
/// The default implementation of the <see cref="ICastleSiegeMachineRegionNotifyPlugIn"/>
/// which forwards warfare-machine impact regions to the game client.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.CastleSiegeMachineRegionNotifyPlugIn_Name), Description = nameof(PlugInResources.CastleSiegeMachineRegionNotifyPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("9056C2A5-7506-47D7-B248-FAB73E5CAB02")]
public class CastleSiegeMachineRegionNotifyPlugIn : ICastleSiegeMachineRegionNotifyPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="CastleSiegeMachineRegionNotifyPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public CastleSiegeMachineRegionNotifyPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public ValueTask ShowMachineRegionAsync(MachineType machineType, Point target)
        => this._player.Connection?.SendCastleSiegeMachineRegionNotifyAsync(
            (MachinePacketType)machineType,
            target.X,
            target.Y) ?? default;
}
