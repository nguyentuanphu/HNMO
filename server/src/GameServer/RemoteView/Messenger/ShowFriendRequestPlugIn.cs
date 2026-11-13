// <copyright file="ShowFriendRequestPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Messenger;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views.Messenger;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IShowFriendRequestPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowFriendRequestPlugIn_Name), Description = nameof(PlugInResources.ShowFriendRequestPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("efb8b51b-181a-4a7d-b5a4-34c0dbd41748")]
public class ShowFriendRequestPlugIn : IShowFriendRequestPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowFriendRequestPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowFriendRequestPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowFriendRequestAsync(string requester)
    {
        await this._player.Connection.SendFriendRequestAsync(requester).ConfigureAwait(false);
    }
}