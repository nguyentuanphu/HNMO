// <copyright file="ChatMessageBaseHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions;
using HNMO.GameLogic.PlayerActions.Chat;
using HNMO.Network.Packets.ClientToServer;

/// <summary>
/// Base class for a chat message handler.
/// </summary>
internal abstract class ChatMessageBaseHandlerPlugIn : IPacketHandlerPlugIn
{
    private readonly ChatMessageAction _messageAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public abstract byte Key { get; }

    /// <summary>
    /// Gets a value indicating whether this instance is handling whisper messages.
    /// </summary>
    protected abstract bool IsWhisper { get; }

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        WhisperMessage message = packet;
        await this._messageAction.ChatMessageAsync(player, message.ReceiverName, message.Message, this.IsWhisper).ConfigureAwait(false);
    }
}