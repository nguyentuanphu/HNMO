// <copyright file="CastleSiegeGuildListPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.CastleSiege;

using System.Runtime.InteropServices;
using HNMO.GameLogic.CastleSiege;
using HNMO.GameLogic.Views.CastleSiege;
using HNMO.Network;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="ICastleSiegeGuildListPlugIn"/>
/// which forwards the participating guild list to the game client.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.CastleSiegeGuildListPlugIn_Name), Description = nameof(PlugInResources.CastleSiegeGuildListPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("7AA5CCA9-F6DE-4A5A-93A0-5049C653809D")]
public class CastleSiegeGuildListPlugIn : ICastleSiegeGuildListPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="CastleSiegeGuildListPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public CastleSiegeGuildListPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowGuildListAsync(
        byte result,
        IReadOnlyCollection<CastleSiegeGuildParticipant> guilds)
    {
        if (this._player.Connection is not { } connection)
        {
            return;
        }

        var guildList = guilds.ToList();

        int Write()
        {
            var size = CastleSiegeGuildListRef.GetRequiredSize(guildList.Count);
            var packet = new CastleSiegeGuildListRef(connection.Output.GetSpan(size)[..size])
            {
                Result = result,
                GuildCount = checked((uint)guildList.Count),
            };

            for (var i = 0; i < guildList.Count; i++)
            {
                var guild = guildList[i];
                var entry = packet[i];
                entry.Side = (CastleSiegeJoinSide)guild.Side;
                entry.IsInvolved = guild.IsAllianceMaster;
                entry.GuildName = guild.GuildName;
                entry.Score = checked((uint)guild.Score);
            }

            return size;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }
}
