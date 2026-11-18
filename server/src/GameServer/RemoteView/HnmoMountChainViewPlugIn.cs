// <copyright file="HnmoMountChainViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Default remote view implementation for <see cref="IHnmoMountChainViewPlugIn"/>.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Mount Chain View", Description = "Sends Mount Chain collection and upgrade progress to web game clients.")]
[Guid("6C8E2B41-3F9A-4012-A7C4-5D8E9B2F1042")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class HnmoMountChainViewPlugIn : IHnmoMountChainViewPlugIn
{
    private readonly RemotePlayer _player;
    private bool _isSupportedByClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HnmoMountChainViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The remote player.</param>
    public HnmoMountChainViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowMountChainAsync(string json, bool requested)
    {
        if (requested)
        {
            this._isSupportedByClient = true;
        }

        if (!this._isSupportedByClient)
        {
            return;
        }

        await this._player.Connection.SendHnmoMountChainStateAsync(json).ConfigureAwait(false);
    }
}
