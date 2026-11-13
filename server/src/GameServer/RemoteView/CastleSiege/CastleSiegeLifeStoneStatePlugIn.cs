// <copyright file="CastleSiegeLifeStoneStatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.CastleSiege;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views.CastleSiege;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="ICastleSiegeLifeStoneStatePlugIn"/>
/// which forwards Life Stone creation progress to the game client.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.CastleSiegeLifeStoneStatePlugIn_Name), Description = nameof(PlugInResources.CastleSiegeLifeStoneStatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("E21D4021-6F9A-4D89-8CA1-32DF76F9C799")]
public sealed class CastleSiegeLifeStoneStatePlugIn : ICastleSiegeLifeStoneStatePlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="CastleSiegeLifeStoneStatePlugIn"/> class.
    /// </summary>
    /// <param name="player">The player receiving the view update.</param>
    public CastleSiegeLifeStoneStatePlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public ValueTask ShowLifeStoneBuildTimeAsync(ushort npcId, byte buildTime)
    {
        return this._player.Connection?.SendCastleSiegeLifeStoneBuildTimeAsync(npcId, buildTime) ?? default;
    }
}
