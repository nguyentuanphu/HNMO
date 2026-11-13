// <copyright file="GuildJoinResponsePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Guild;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views.Guild;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IGuildJoinResponsePlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.GuildJoinResponsePlugIn_Name), Description = nameof(PlugInResources.GuildJoinResponsePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("4a8bd97c-a544-4cac-b8cd-2e73945bcfdc")]
public class GuildJoinResponsePlugIn : IGuildJoinResponsePlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="GuildJoinResponsePlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public GuildJoinResponsePlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowGuildJoinResponseAsync(GuildRequestAnswerResult result)
    {
        await this._player.Connection.SendGuildJoinResponseAsync(result.Convert()).ConfigureAwait(false);
    }
}