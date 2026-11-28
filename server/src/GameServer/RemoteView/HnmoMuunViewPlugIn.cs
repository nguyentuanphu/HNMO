// <copyright file="HnmoMuunViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>Sends the Season 9 Muun state (F5 2C) to a web game client which asked for it once.</summary>
[PlugIn]
[Display(Name = "HNMO Muun View", Description = "Sends the Season 9 Muun state to web game clients.")]
[Guid("6F2B8C41-9D35-4E7A-B0C8-52A1E7F3D914")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class HnmoMuunViewPlugIn : IHnmoMuunViewPlugIn
{
    private readonly RemotePlayer _player;
    private bool _isSupportedByClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HnmoMuunViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The remote player.</param>
    public HnmoMuunViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowMuunAsync(string json, bool requested)
    {
        if (requested)
        {
            this._isSupportedByClient = true;
        }

        if (!this._isSupportedByClient)
        {
            return;
        }

        await this._player.Connection.SendHnmoMuunStateAsync(json).ConfigureAwait(false);
    }
}
