// <copyright file="HnmoLeftoversCommandHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.CombiningLeftovers;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Packet handler for <see cref="HnmoLeftoversCommand"/> (<c>F5 40</c>).
/// </summary>
[PlugIn]
[Display(Name = "HNMO Leftovers Command Handler", Description = "Handles leftover upgrade combines and surplus material exchanges.")]
[Guid("2F4B8A91-3C12-47D0-95E8-1A7C6D4E2B03")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class HnmoLeftoversCommandHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => HnmoLeftoversCommand.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null || CombiningLeftoversPlugIn.Get(player.GameContext) is not { } plugIn)
        {
            return;
        }

        HnmoLeftoversCommand cmd = packet;
        switch (cmd.Action)
        {
            case 0:
                await plugIn.SendStateAsync(player, requested: true).ConfigureAwait(false);
                break;
            case 1:
                await plugIn.CombineAsync(player, cmd.Id ?? string.Empty).ConfigureAwait(false);
                break;
            case 2:
                await plugIn.ExchangeAsync(player, cmd.Id ?? string.Empty).ConfigureAwait(false);
                break;
            default:
                await plugIn.SendStateAsync(player, requested: true).ConfigureAwait(false);
                break;
        }
    }
}
