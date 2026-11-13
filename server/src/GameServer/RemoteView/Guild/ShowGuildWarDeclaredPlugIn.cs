// <copyright file="ShowGuildWarDeclaredPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Guild;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views.Guild;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IShowGuildWarDeclaredPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowGuildWarDeclaredPlugIn_Name), Description = nameof(PlugInResources.ShowGuildWarDeclaredPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("50393D5D-01F2-43F8-B5D7-243D91B905BC")]
public class ShowGuildWarDeclaredPlugIn : IShowGuildWarDeclaredPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowGuildWarDeclaredPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowGuildWarDeclaredPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowDeclaredAsync()
    {
        if (this._player.GuildWarContext is { } guildWarContext)
        {
            await this._player.Connection.SendGuildWarDeclaredAsync(guildWarContext.EnemyTeamName, guildWarContext.WarType.Convert(), (byte)guildWarContext.Team).ConfigureAwait(false);
        }
    }
}