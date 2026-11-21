// <copyright file="HnmoVaultPageViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Sends <see cref="HnmoVaultPageState"/> (<c>F5 4F</c>) packets to web clients. Only a client which asked once
/// (<c>F5 4E</c>) gets them.
/// </summary>
[PlugIn]
[Display(Name = "HNMO vault pages view", Description = "Sends the vault pages (page in use, unlocked pages, price) to web clients.")]
[Guid("8D3F1A62-5B7E-4C09-A2D4-6E91B0C3F7A5")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public sealed class HnmoVaultPageViewPlugIn : IHnmoVaultPageViewPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>Initializes a new instance of the <see cref="HnmoVaultPageViewPlugIn"/> class.</summary>
    /// <param name="player">The remote player.</param>
    public HnmoVaultPageViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowVaultPagesAsync(string json)
    {
        await this._player.Connection.SendHnmoVaultPageStateAsync(json).ConfigureAwait(false);
    }
}
