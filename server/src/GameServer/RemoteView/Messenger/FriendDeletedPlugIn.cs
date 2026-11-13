// <copyright file="FriendDeletedPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Messenger;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views.Messenger;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IFriendDeletedPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.FriendDeletedPlugIn_Name), Description = nameof(PlugInResources.FriendDeletedPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("3f6f86ec-ffe8-4fa7-82c3-4a743fab7157")]
public class FriendDeletedPlugIn : IFriendDeletedPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="FriendDeletedPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public FriendDeletedPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask FriendDeletedAsync(string deletedFriend)
    {
        await this._player.Connection.SendFriendDeletedAsync(deletedFriend).ConfigureAwait(false);
    }
}