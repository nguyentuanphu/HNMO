// <copyright file="HnmoMarketViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Sends <see cref="HnmoMarketState"/> packets (<c>F5 39</c>) to clients supporting Item Market.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Item Market view", Description = "Sends escrow item market listings and state packets to the client.")]
[Guid("7F2E8D6C-49A1-4B38-8C10-9B64E2D5F1A7")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public sealed class HnmoMarketViewPlugIn : IHnmoMarketViewPlugIn
{
    private readonly RemotePlayer _player;
    private bool _isSupportedByClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HnmoMarketViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The remote player.</param>
    public HnmoMarketViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowMarketAsync(string json, bool requested)
    {
        if (requested)
        {
            this._isSupportedByClient = true;
        }

        if (!this._isSupportedByClient)
        {
            return;
        }

        await this._player.Connection.SendHnmoMarketStateAsync(json).ConfigureAwait(false);
    }
}
