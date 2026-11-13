// <copyright file="ItemPriceSetResponsePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Inventory;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views.Inventory;
using HNMO.GameLogic.Views.PlayerShop;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IItemPriceSetResponsePlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ItemPriceSetResponsePlugIn_Name), Description = nameof(PlugInResources.ItemPriceSetResponsePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("f83daf12-28cb-47bc-bb23-7f8eba21c97c")]
public class ItemPriceSetResponsePlugIn : IItemPriceSetResponsePlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemPriceSetResponsePlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ItemPriceSetResponsePlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ItemPriceSetResponseAsync(byte itemSlot, ItemPriceResult result)
    {
        await this._player.Connection.SendPlayerShopSetItemPriceResponseAsync(itemSlot, result.Convert()).ConfigureAwait(false);
    }
}