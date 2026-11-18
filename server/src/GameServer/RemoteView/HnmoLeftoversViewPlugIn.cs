// <copyright file="HnmoLeftoversViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Default remote view implementation for <see cref="IHnmoLeftoversViewPlugIn"/>.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Leftovers View", Description = "Sends Combining Leftovers & Surplus Material Exchange state to web game clients.")]
[Guid("9D7A2E14-5B81-4A63-9F02-8C4D1E72A310")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class HnmoLeftoversViewPlugIn : IHnmoLeftoversViewPlugIn
{
    private readonly RemotePlayer _player;
    private bool _isSupportedByClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HnmoLeftoversViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The remote player.</param>
    public HnmoLeftoversViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowLeftoversAsync(string json, bool requested)
    {
        if (requested)
        {
            this._isSupportedByClient = true;
        }

        if (!this._isSupportedByClient)
        {
            return;
        }

        await this._player.Connection.SendHnmoLeftoversStateAsync(json).ConfigureAwait(false);
    }
}
