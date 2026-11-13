// <copyright file="ShowGuildJoinRequestPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Guild;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.Views.Guild;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IShowGuildJoinRequestPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowGuildJoinRequestPlugIn_Name), Description = nameof(PlugInResources.ShowGuildJoinRequestPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("521ff03d-c8ad-44d9-a23d-8f98c4c174ae")]
public class ShowGuildJoinRequestPlugIn : IShowGuildJoinRequestPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowGuildJoinRequestPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowGuildJoinRequestPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowGuildJoinRequestAsync(Player requester)
    {
        await this._player.Connection.SendGuildJoinRequestAsync(requester.Id).ConfigureAwait(false);
    }
}