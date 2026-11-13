// <copyright file="AddFriendHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Messenger;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Messenger;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Handler for add friend packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.AddFriendHandlerPlugIn_Name), Description = nameof(PlugInResources.AddFriendHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("302870db-59cc-4cf8-b5ed-b0efa9f6ccbc")]
internal class AddFriendHandlerPlugIn : IPacketHandlerPlugIn
{
    private readonly AddFriendAction _addAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => FriendAddRequest.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        FriendAddRequest message = packet;
        await this._addAction.AddFriendAsync(player, message.FriendName).ConfigureAwait(false);
    }
}