// <copyright file="ItemUpgradedPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Inventory;

using System.Runtime.InteropServices;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.Views.Inventory;
using HNMO.Network;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IItemUpgradedPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ItemUpgradedPlugIn_Name), Description = nameof(PlugInResources.ItemUpgradedPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("ce4ed0a2-ec4e-4cbe-aabe-5573df86a659")]
public class ItemUpgradedPlugIn : IItemUpgradedPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemUpgradedPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ItemUpgradedPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ItemUpgradedAsync(Item item)
    {
        var connection = this._player.Connection;
        if (connection is null)
        {
            return;
        }

        int Write()
        {
            var itemSerializer = this._player.ItemSerializer;
            var size = InventoryItemUpgradedRef.GetRequiredSize(itemSerializer.NeededSpace);
            var span = connection.Output.GetSpan(size)[..size];
            var packet = new InventoryItemUpgradedRef(span)
            {
                InventorySlot = item.ItemSlot,
            };
            var itemSize = itemSerializer.SerializeItem(packet.ItemData, item);
            var actualSize = InventoryItemUpgradedRef.GetRequiredSize(itemSize);
            span.Slice(0, actualSize).SetPacketSize();
            return actualSize;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }
}