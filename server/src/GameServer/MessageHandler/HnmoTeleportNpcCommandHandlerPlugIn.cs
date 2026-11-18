// <copyright file="HnmoTeleportNpcCommandHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.TeleportNpc;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Packet handler for <see cref="HnmoTeleportNpcCommand"/> (<c>F5 4A</c>).
/// </summary>
[PlugIn]
[Display(Name = "HNMO Teleport NPC Command Handler", Description = "Handles Teleport NPC destination list requests and warp actions.")]
[Guid("E4A7C320-5D91-4B68-8F02-9C4E1A7D2B83")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class HnmoTeleportNpcCommandHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => HnmoTeleportNpcCommand.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null || TeleportNpcPlugIn.Get(player.GameContext) is not { } plugIn)
        {
            return;
        }

        HnmoTeleportNpcCommand cmd = packet;
        await plugIn.HandleCommandAsync(player, cmd.Action, cmd.DestinationId).ConfigureAwait(false);
    }
}
