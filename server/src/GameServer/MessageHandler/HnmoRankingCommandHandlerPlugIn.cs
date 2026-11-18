// <copyright file="HnmoRankingCommandHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.PlayerRanking;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Packet handler for <see cref="HnmoRankingCommand"/> (<c>F5 46</c>).
/// </summary>
[PlugIn]
[Display(Name = "HNMO Ranking Command Handler", Description = "Handles in-game ranking snapshot requests and bulk add-stats submissions.")]
[Guid("9A4D6F20-2E1B-4C84-8D51-7E3B1C0F6A98")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class HnmoRankingCommandHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => HnmoRankingCommand.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null || PlayerRankingPlugIn.Get(player.GameContext) is not { } plugIn)
        {
            return;
        }

        HnmoRankingCommand cmd = packet;
        switch (cmd.Action)
        {
            case 1:
                await plugIn.HandleBulkAddStatsAsync(player, cmd.Payload).ConfigureAwait(false);
                break;
            default:
                await plugIn.SendStateAsync(player, requested: true).ConfigureAwait(false);
                break;
        }
    }
}
