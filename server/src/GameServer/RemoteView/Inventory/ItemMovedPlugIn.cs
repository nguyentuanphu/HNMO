// <copyright file="ItemMovedPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Inventory;

using System.Runtime.InteropServices;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Views.Inventory;
using HNMO.Network;
using HNMO.Network.Packets;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IItemMovedPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ItemMovedPlugIn_Name), Description = nameof(PlugInResources.ItemMovedPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("5c4c20fe-763d-42b6-bdfa-2ec943b191bc")]
public class ItemMovedPlugIn : IItemMovedPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemMovedPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ItemMovedPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ItemMovedAsync(Item item, byte toSlot, Storages storage)
    {
        var connection = this._player.Connection;
        if (connection is null)
        {
            return;
        }

        var itemSerializer = this._player.ItemSerializer;
        var targetStorage = storage.Convert();
        if (targetStorage == ItemStorageKind.PlayerShop)
        {
            targetStorage = ItemStorageKind.Inventory;
        }

        int Write()
        {
            var size = ItemMovedRef.GetRequiredSize(itemSerializer.NeededSpace);
            var span = connection.Output.GetSpan(size)[..size];
            var message = new ItemMovedRef(span)
            {
                TargetStorageType = targetStorage,
                TargetSlot = toSlot,
            };
            var itemSize = itemSerializer.SerializeItem(message.ItemData, item);

            var actualSize = ItemMovedRef.GetRequiredSize(itemSize);
            span.Slice(0, actualSize).SetPacketSize();
            return actualSize;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }
}