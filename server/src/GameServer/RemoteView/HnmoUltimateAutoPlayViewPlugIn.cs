// <copyright file="HnmoUltimateAutoPlayViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Default remote view implementation for <see cref="IHnmoUltimateAutoPlayViewPlugIn"/>.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Ultimate Autoplay View", Description = "Sends Ultimate Autoplay Free vs VIP policy to web clients.")]
[Guid("E2F49A13-7D8B-4A6C-B3E1-9F4A2C7D6018")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class HnmoUltimateAutoPlayViewPlugIn : IHnmoUltimateAutoPlayViewPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>Initializes a new instance of the <see cref="HnmoUltimateAutoPlayViewPlugIn"/> class.</summary>
    /// <param name="player">The remote player.</param>
    public HnmoUltimateAutoPlayViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowUltimateAutoPlayPolicyAsync(string json)
    {
        await this._player.Connection.SendHnmoUltimateAutoPlayPolicyAsync(json).ConfigureAwait(false);
    }
}
