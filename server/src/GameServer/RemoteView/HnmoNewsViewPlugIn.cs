// <copyright file="HnmoNewsViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Default remote view implementation for <see cref="IHnmoNewsViewPlugIn"/>.
/// </summary>
[PlugIn]
[Display(Name = "HNMO News Notice View", Description = "Sends HNMO news notices to web game clients.")]
[Guid("B71F4802-2B8F-45BA-9E93-C045E4A42B1E")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class HnmoNewsViewPlugIn : IHnmoNewsViewPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="HnmoNewsViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The remote player.</param>
    public HnmoNewsViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowNewsNoticeAsync(string json)
    {
        if (this._player.Connection is not { Connected: true } connection)
        {
            return;
        }

        await connection.SendHnmoNewsNoticeAsync(json).ConfigureAwait(false);
    }
}
