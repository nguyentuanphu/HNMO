// <copyright file="HnmoLuckyNumberViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Sends <see cref="HnmoLuckyNumberState"/> packets (<c>F5 35</c>) to clients supporting Daily Lucky Number.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Lucky number view", Description = "Sends daily lucky number state packets to the client.")]
[Guid("1D9E7C42-5A18-4B3D-9E6F-8C40A2B1D5F0")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public sealed class HnmoLuckyNumberViewPlugIn : IHnmoLuckyNumberViewPlugIn
{
    private readonly RemotePlayer _player;
    private bool _isSupportedByClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HnmoLuckyNumberViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The remote player.</param>
    public HnmoLuckyNumberViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowLuckyNumberAsync(string json, bool requested)
    {
        if (requested)
        {
            this._isSupportedByClient = true;
        }

        if (!this._isSupportedByClient)
        {
            return;
        }

        await this._player.Connection.SendHnmoLuckyNumberStateAsync(json).ConfigureAwait(false);
    }
}
