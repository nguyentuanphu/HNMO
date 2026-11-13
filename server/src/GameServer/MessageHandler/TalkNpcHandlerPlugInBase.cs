// <copyright file="TalkNpcHandlerPlugInBase.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using HNMO.GameLogic;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlayerActions;
using HNMO.Network.Packets.ClientToServer;

/// <summary>
/// Handler for talk npc request packets.
/// </summary>
internal abstract class TalkNpcHandlerPlugInBase : IPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public virtual bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => TalkToNpcRequest.Code;

    /// <summary>
    /// Gets the talk NPC action.
    /// </summary>
    protected abstract TalkNpcAction TalkNpcAction { get; }

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        TalkToNpcRequest message = packet;
        if (player.CurrentMap?.GetObject(message.NpcId) is NonPlayerCharacter npc)
        {
            await this.TalkNpcAction.TalkToNpcAsync(player, npc).ConfigureAwait(false);
        }
    }
}