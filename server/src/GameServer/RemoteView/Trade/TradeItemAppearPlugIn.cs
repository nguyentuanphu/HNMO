// <copyright file="TradeItemAppearPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Trade;

using System.Runtime.InteropServices;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.Views.Trade;
using HNMO.Network;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="ITradeItemAppearPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.TradeItemAppearPlugIn_Name), Description = nameof(PlugInResources.TradeItemAppearPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("560d18e7-7d36-47bf-9298-8017454fa7bf")]
public class TradeItemAppearPlugIn : ITradeItemAppearPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="TradeItemAppearPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public TradeItemAppearPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask TradeItemAppearAsync(byte toSlot, Item item)
    {
        var connection = this._player.Connection;
        if (connection is null)
        {
            return;
        }

        int Write()
        {
            var itemSerializer = this._player.ItemSerializer;
            var size = TradeItemAddedRef.GetRequiredSize(itemSerializer.NeededSpace);
            var span = connection.Output.GetSpan(size)[..size];
            var packet = new TradeItemAddedRef(span)
            {
                ToSlot = toSlot,
            };
            var itemSize = itemSerializer.SerializeItem(packet.ItemData, item);
            var actualSize = TradeItemAddedRef.GetRequiredSize(itemSize);
            span.Slice(0, actualSize).SetPacketSize();
            return actualSize;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }
}