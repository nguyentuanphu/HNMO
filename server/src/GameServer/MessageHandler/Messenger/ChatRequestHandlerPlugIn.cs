// <copyright file="ChatRequestHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Messenger;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Messenger;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Handler for chat request packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ChatRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.ChatRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("acf9263f-ba71-4d84-b8f8-84e494eb4462")]
internal class ChatRequestHandlerPlugIn : IPacketHandlerPlugIn
{
    private readonly ChatRequestAction _chatRequestAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => ChatRoomCreateRequest.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        ChatRoomCreateRequest message = packet;
        await this._chatRequestAction.RequestChatAsync(player, message.FriendName).ConfigureAwait(false);
    }
}