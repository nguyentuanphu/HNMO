// <copyright file="CastleSiegeJoinSidePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.CastleSiege;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views.CastleSiege;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;
using JoinSide = HNMO.DataModel.Configuration.CastleSiegeJoinSide;

/// <summary>
/// The default implementation of the <see cref="ICastleSiegeJoinSidePlugIn"/>
/// which forwards the assigned side to the game client.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.CastleSiegeJoinSidePlugIn_Name), Description = nameof(PlugInResources.CastleSiegeJoinSidePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("175ECFB8-476A-4371-A416-A1B1C5E19402")]
public class CastleSiegeJoinSidePlugIn : ICastleSiegeJoinSidePlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="CastleSiegeJoinSidePlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public CastleSiegeJoinSidePlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public ValueTask ShowJoinSideAsync(JoinSide side)
        => this._player.Connection?.SendCastleSiegeJoinSideNotificationAsync((CastleSiegeJoinSide)side) ?? default;
}
