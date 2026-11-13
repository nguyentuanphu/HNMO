// <copyright file="TeleportPlugIn.cs" company="HNMO">
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
/// The default implementation of the <see cref="ITeleportPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.TeleportPlugIn_Name), Description = nameof(PlugInResources.TeleportPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("9506F77B-CA72-4150-87E3-57C889C91F02")]
[MinimumClient(1, 0, ClientLanguage.Invariant)]
public class TeleportPlugIn : ITeleportPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeleportPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public TeleportPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowTeleportedAsync()
    {
        if (this._player.SelectedCharacter?.CurrentMap is null)
        {
            return;
        }

        var mapNumber = this._player.SelectedCharacter.CurrentMap.Number.ToUnsigned();
        var position = this._player.Position;
        await this._player.Connection.SendMapChangedAsync(mapNumber, position.X, position.Y, this._player.Rotation.ToPacketByte(), false).ConfigureAwait(false);
    }
}