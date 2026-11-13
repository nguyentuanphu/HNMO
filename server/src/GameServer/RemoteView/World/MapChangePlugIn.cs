// <copyright file="MapChangePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.World;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views.World;
using HNMO.Network;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IMapChangePlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.MapChangePlugIn_Name), Description = nameof(PlugInResources.MapChangePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("234b477d-6fe9-4caa-a03f-78cb25518b39")]
[MinimumClient(1, 0, ClientLanguage.Invariant)]
public class MapChangePlugIn : IMapChangePlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="MapChangePlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public MapChangePlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public ValueTask MapChangeAsync()
    {
        return this.SendMessageAsync(true);
    }

    /// <inheritdoc/>
    public ValueTask MapChangeFailedAsync()
    {
        return this.SendMessageAsync(false);
    }

    private async ValueTask SendMessageAsync(bool success)
    {
        if (this._player.SelectedCharacter?.CurrentMap is null)
        {
            return;
        }

        var mapNumber = this._player.SelectedCharacter.CurrentMap.Number.ToUnsigned();
        var position = this._player.IsWalking ? this._player.WalkTarget : this._player.Position;

        await this._player.Connection.SendMapChangedAsync(mapNumber, position.X, position.Y, this._player.Rotation.ToPacketByte(), success).ConfigureAwait(false);
    }
}