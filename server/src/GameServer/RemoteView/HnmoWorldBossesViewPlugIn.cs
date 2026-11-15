// <copyright file="HnmoWorldBossesViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IHnmoWorldBossesViewPlugIn"/> which sends HNMO's world bosses state to the
/// game client. Unasked updates only go to a client which requested the world bosses once (the web game).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoWorldBossesViewPlugIn_Name), Description = nameof(PlugInResources.HnmoWorldBossesViewPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("5E2B8C41-9D73-4A06-B1F5-3C8E0D6A92B7")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class HnmoWorldBossesViewPlugIn : IHnmoWorldBossesViewPlugIn
{
    private readonly RemotePlayer _player;
    private bool _isSupportedByClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HnmoWorldBossesViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public HnmoWorldBossesViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowWorldBossesAsync(string json, bool requested)
    {
        if (requested)
        {
            this._isSupportedByClient = true;
        }

        if (!this._isSupportedByClient)
        {
            return;
        }

        await this._player.Connection.SendHnmoWorldBossesStateAsync(json).ConfigureAwait(false);
    }
}
