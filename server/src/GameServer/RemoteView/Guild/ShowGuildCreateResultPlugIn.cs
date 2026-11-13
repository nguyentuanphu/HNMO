// <copyright file="ShowGuildCreateResultPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Guild;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views.Guild;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IShowGuildCreateResultPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowGuildCreateResultPlugIn_Name), Description = nameof(PlugInResources.ShowGuildCreateResultPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("ff6c5a06-4699-461b-9004-756269393c40")]
public class ShowGuildCreateResultPlugIn : IShowGuildCreateResultPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowGuildCreateResultPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowGuildCreateResultPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowGuildCreateResultAsync(GuildCreateErrorDetail errorDetail)
    {
        await this._player.Connection.SendGuildCreationResultAsync(errorDetail == GuildCreateErrorDetail.None, errorDetail.Convert()).ConfigureAwait(false);
    }
}