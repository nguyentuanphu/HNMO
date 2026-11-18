// <copyright file="HnmoEscrowViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Sends <see cref="HnmoEscrowState"/> (<c>F5 4D</c>) packets to connected web clients.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Escrow Contracts view", Description = "Pushes escrow contracts and owed delivery state to web clients.")]
[Guid("2B4C6D8E-1F3A-4B5C-9D0E-2A4B6C8D0E1F")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public sealed class HnmoEscrowViewPlugIn : IHnmoEscrowViewPlugIn
{
    private readonly RemotePlayer _player;
    private bool _isSupportedByClient;

    /// <summary>Initializes a new instance of the <see cref="HnmoEscrowViewPlugIn"/> class.</summary>
    /// <param name="player">The remote player.</param>
    public HnmoEscrowViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowEscrowAsync(string json, bool requested)
    {
        if (requested)
        {
            this._isSupportedByClient = true;
        }

        if (!this._isSupportedByClient)
        {
            return;
        }

        await this._player.Connection.SendHnmoEscrowStateAsync(json).ConfigureAwait(false);
    }
}
