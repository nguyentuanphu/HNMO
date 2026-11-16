// <copyright file="HnmoTeleportNpcViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Default remote view implementation for <see cref="IHnmoTeleportNpcViewPlugIn"/>.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Teleport NPC View", Description = "Delivers Teleport NPC destination options and warp status to web clients.")]
[Guid("B9E4A210-6F38-4C7A-8B15-4D2E9C7A1F60")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class HnmoTeleportNpcViewPlugIn : IHnmoTeleportNpcViewPlugIn
{
    private readonly RemotePlayer _player;
    private bool _isSupportedByClient;

    /// <summary>Initializes a new instance of the <see cref="HnmoTeleportNpcViewPlugIn"/> class.</summary>
    /// <param name="player">The remote player.</param>
    public HnmoTeleportNpcViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowTeleportNpcStateAsync(string json, bool requested = true)
    {
        if (requested)
        {
            this._isSupportedByClient = true;
        }

        if (!this._isSupportedByClient)
        {
            return;
        }

        await this._player.Connection.SendHnmoTeleportNpcStateAsync(json).ConfigureAwait(false);
    }
}
