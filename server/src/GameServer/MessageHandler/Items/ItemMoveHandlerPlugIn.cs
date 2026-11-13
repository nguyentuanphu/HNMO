// <copyright file="ItemMoveHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Items;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Items;
using HNMO.GameServer.RemoteView;
using HNMO.GameServer.RemoteView.Inventory;
using HNMO.Network.Packets;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Handler for item move packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ItemMoveHandlerPlugIn_Name), Description = nameof(PlugInResources.ItemMoveHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("c499c596-7711-4971-bc83-7abd9e6b5553")]
internal class ItemMoveHandlerPlugIn : IPacketHandlerPlugIn
{
    private readonly MoveItemAction _moveAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => ItemMoveRequest.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        ItemMoveRequest message = packet;

        // to make it compatible with multiple versions, we just handle the data which is coming after that manually
        var itemSize = 12;
        if (player is RemotePlayer remotePlayer)
        {
            itemSize = remotePlayer.ItemSerializer.NeededSpace;
        }

        var toStorage = (ItemStorageKind)packet.Span[5 + itemSize];
        byte toSlot = packet.Span[6 + itemSize];

        await this._moveAction.MoveItemAsync(player, message.FromSlot, message.FromStorage.Convert(), toSlot, toStorage.Convert()).ConfigureAwait(false);
    }
}