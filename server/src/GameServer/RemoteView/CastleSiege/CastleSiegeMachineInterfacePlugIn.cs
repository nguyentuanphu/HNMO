// <copyright file="CastleSiegeMachineInterfacePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.CastleSiege;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views.CastleSiege;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;
using MachinePacketType = HNMO.Network.Packets.ServerToClient.CastleSiegeMachineType;
using MachineType = HNMO.GameLogic.CastleSiege.CastleSiegeMachineType;

/// <summary>
/// The default implementation of the <see cref="ICastleSiegeMachineInterfacePlugIn"/>
/// which opens the warfare-machine interface on the game client.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.CastleSiegeMachineInterfacePlugIn_Name), Description = nameof(PlugInResources.CastleSiegeMachineInterfacePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("60F1C3AC-2A7A-44B1-8DE1-8CB030E1A38A")]
public class CastleSiegeMachineInterfacePlugIn : ICastleSiegeMachineInterfacePlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="CastleSiegeMachineInterfacePlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public CastleSiegeMachineInterfacePlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public ValueTask ShowMachineInterfaceAsync(bool success, MachineType machineType, ushort machineId)
        => this._player.Connection?.SendCastleSiegeMachineInterfaceAsync(
            success ? (byte)1 : (byte)0,
            (MachinePacketType)machineType,
            machineId) ?? default;
}
