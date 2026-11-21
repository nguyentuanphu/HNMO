// <copyright file="HnmoItemStackViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>Sends <see cref="HnmoItemStackState"/> (<c>F5 51</c>) packets to web clients.</summary>
[PlugIn]
[Display(Name = "HNMO item stacks view", Description = "Sends the item stack kinds and unlocks to web clients.")]
[Guid("5E81C2D7-3A94-4B6F-8C05-9D2A7E4B1F63")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public sealed class HnmoItemStackViewPlugIn : IHnmoItemStackViewPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>Initializes a new instance of the <see cref="HnmoItemStackViewPlugIn"/> class.</summary>
    /// <param name="player">The remote player.</param>
    public HnmoItemStackViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowItemStacksAsync(string json)
    {
        await this._player.Connection.SendHnmoItemStackStateAsync(json).ConfigureAwait(false);
    }
}
