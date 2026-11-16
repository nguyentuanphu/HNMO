// <copyright file="HnmoCatchupCapViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Default remote view implementation for <see cref="IHnmoCatchupCapViewPlugIn"/>.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Catch-up Cap View", Description = "Sends today's Catch-up Level/Reset Cap state to web game clients.")]
[Guid("2F3E4A18-7C5D-4B9E-8F12-6D5B4C3E2A10")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class HnmoCatchupCapViewPlugIn : IHnmoCatchupCapViewPlugIn
{
    private readonly RemotePlayer _player;
    private bool _isSupportedByClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HnmoCatchupCapViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The remote player.</param>
    public HnmoCatchupCapViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowCatchupCapAsync(string json, bool requested)
    {
        if (requested)
        {
            this._isSupportedByClient = true;
        }

        if (!this._isSupportedByClient)
        {
            return;
        }

        await this._player.Connection.SendHnmoCatchupCapStateAsync(json).ConfigureAwait(false);
    }
}
