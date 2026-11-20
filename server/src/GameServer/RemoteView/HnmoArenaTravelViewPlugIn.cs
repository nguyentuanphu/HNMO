// <copyright file="HnmoArenaTravelViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>Sends <see cref="HnmoArenaTravelState"/> (<c>F5 53</c>) packets to web clients.</summary>
[PlugIn]
[Display(Name = "HNMO Arena travel view", Description = "Sends the Arena's travel state and travel tickets to web clients.")]
[Guid("6A2D9F14-8C3B-4E71-B5A0-1F7E3C9D2B86")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public sealed class HnmoArenaTravelViewPlugIn : IHnmoArenaTravelViewPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>Initializes a new instance of the <see cref="HnmoArenaTravelViewPlugIn"/> class.</summary>
    /// <param name="player">The remote player.</param>
    public HnmoArenaTravelViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowArenaTravelAsync(string json)
    {
        await this._player.Connection.SendHnmoArenaTravelStateAsync(json).ConfigureAwait(false);
    }
}
