// <copyright file="HnmoGuildsViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IHnmoGuildsViewPlugIn"/> which sends HNMO's guild levels state to the
/// game client. Unasked updates only go to a client which requested the guild levels once (the web game).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoGuildsViewPlugIn_Name), Description = nameof(PlugInResources.HnmoGuildsViewPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("A3C91F07-5E2D-4B86-9D14-7F0E6B28C5D3")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class HnmoGuildsViewPlugIn : IHnmoGuildsViewPlugIn
{
    private readonly RemotePlayer _player;
    private bool _isSupportedByClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HnmoGuildsViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public HnmoGuildsViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowGuildsAsync(string json, bool requested)
    {
        if (requested)
        {
            this._isSupportedByClient = true;
        }

        if (!this._isSupportedByClient)
        {
            return;
        }

        await this._player.Connection.SendHnmoGuildsStateAsync(json).ConfigureAwait(false);
    }
}
