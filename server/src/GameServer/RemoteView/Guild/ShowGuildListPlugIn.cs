// <copyright file="ShowGuildListPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Guild;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views.Guild;
using HNMO.Network;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IShowGuildListPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowGuildListPlugIn_Name), Description = nameof(PlugInResources.ShowGuildListPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("f72a9968-100b-481f-aba1-1dd597fdad47")]
[MinimumClient(0, 90, ClientLanguage.Invariant)]
public class ShowGuildListPlugIn : IShowGuildListPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowGuildListPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowGuildListPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowGuildListAsync(IReadOnlyCollection<HNMO.Interfaces.GuildListEntry> players, Interfaces.Guild guild)
    {
        var connection = this._player.Connection;
        if (connection is null)
        {
            return;
        }

        var playerCount = players.Count;
        int Write()
        {
            var size = GuildListRef.GetRequiredSize(playerCount);
            var span = connection.Output.GetSpan(size)[..size];
            var packet = new GuildListRef(span)
            {
                GuildMemberCount = (byte)playerCount,
                IsInGuild = playerCount > 0,
                RivalGuildName = guild.Hostility?.Name ?? string.Empty,
                CurrentScore = 0, // TODO: What is this? Maybe a rank?
                TotalScore = (uint)guild.Score,
            };

            int i = 0;
            foreach (var member in players)
            {
                var memberBlock = packet[i];
                memberBlock.Name = member.PlayerName;
                memberBlock.Role = member.PlayerPosition.Convert();
                memberBlock.ServerId = member.ServerId;
                memberBlock.ServerId2 = (byte)(member.ServerId == 0xFF ? 0x7F : 0x80 + member.ServerId);
                i++;
            }

            return size;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }
}