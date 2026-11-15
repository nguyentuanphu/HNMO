// <copyright file="HnmoCommunityViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IHnmoCommunityViewPlugIn"/> which sends HNMO's community state to the
/// game client. Unasked updates only go to a client which requested the community state once (the web game).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoCommunityViewPlugIn_Name), Description = nameof(PlugInResources.HnmoCommunityViewPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("5B1E9C47-2A6D-4F83-B0C5-9E4D7A2F1C68")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class HnmoCommunityViewPlugIn : IHnmoCommunityViewPlugIn
{
    private readonly RemotePlayer _player;
    private bool _isSupportedByClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HnmoCommunityViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public HnmoCommunityViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowCommunityAsync(string json, bool requested)
    {
        if (requested)
        {
            this._isSupportedByClient = true;
        }

        if (!this._isSupportedByClient)
        {
            return;
        }

        await this._player.Connection.SendHnmoCommunityStateAsync(json).ConfigureAwait(false);
    }
}
