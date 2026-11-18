// <copyright file="HnmoCustomRanksViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Default remote view implementation for <see cref="IHnmoCustomRanksViewPlugIn"/>.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Custom Ranks View", Description = "Sends custom rank definitions and active player title snapshots to web game clients.")]
[Guid("C4E78B12-3D95-4F62-8A10-5B3E9D2C4F81")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class HnmoCustomRanksViewPlugIn : IHnmoCustomRanksViewPlugIn
{
    private readonly RemotePlayer _player;
    private bool _isSupportedByClient;

    /// <summary>Initializes a new instance of the <see cref="HnmoCustomRanksViewPlugIn"/> class.</summary>
    /// <param name="player">The remote player.</param>
    public HnmoCustomRanksViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowCustomRanksStateAsync(string json, bool requested = true)
    {
        if (requested)
        {
            this._isSupportedByClient = true;
        }

        if (!this._isSupportedByClient)
        {
            return;
        }

        await this._player.Connection.SendHnmoCustomRanksStateAsync(json).ConfigureAwait(false);
    }
}
