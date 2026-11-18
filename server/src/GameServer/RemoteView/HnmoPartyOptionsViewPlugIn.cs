// <copyright file="HnmoPartyOptionsViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Default remote view implementation for <see cref="IHnmoPartyOptionsViewPlugIn"/>.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Party Options View", Description = "Sends Party password & auto-party spot finder listings to web game clients.")]
[Guid("3D2F6A91-8C4B-4105-9E12-7A4D8F3E105C")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class HnmoPartyOptionsViewPlugIn : IHnmoPartyOptionsViewPlugIn
{
    private readonly RemotePlayer _player;
    private bool _isSupportedByClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HnmoPartyOptionsViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The remote player.</param>
    public HnmoPartyOptionsViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowPartyOptionsAsync(string json, bool requested)
    {
        if (requested)
        {
            this._isSupportedByClient = true;
        }

        if (!this._isSupportedByClient)
        {
            return;
        }

        await this._player.Connection.SendHnmoPartyOptionsStateAsync(json).ConfigureAwait(false);
    }
}
