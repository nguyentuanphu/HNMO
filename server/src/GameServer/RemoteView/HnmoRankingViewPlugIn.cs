// <copyright file="HnmoRankingViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Default remote view implementation for <see cref="IHnmoRankingViewPlugIn"/>.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Ranking View", Description = "Sends in-game player ranking and bulk add-stats status snapshots to web game clients.")]
[Guid("1D8E3C24-7F5B-4E19-A60D-4C2B9A8E31F7")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class HnmoRankingViewPlugIn : IHnmoRankingViewPlugIn
{
    private readonly RemotePlayer _player;
    private bool _isSupportedByClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HnmoRankingViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The remote player.</param>
    public HnmoRankingViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowRankingStateAsync(string json, bool requested = true)
    {
        if (requested)
        {
            this._isSupportedByClient = true;
        }

        if (!this._isSupportedByClient)
        {
            return;
        }

        await this._player.Connection.SendHnmoRankingStateAsync(json).ConfigureAwait(false);
    }
}
