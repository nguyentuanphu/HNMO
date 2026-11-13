// <copyright file="ItemDurabilityChangedPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Inventory;

using System.Runtime.InteropServices;
using HNMO.DataModel;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.Views.Inventory;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IItemDurabilityChangedPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ItemDurabilityChangedPlugIn_Name), Description = nameof(PlugInResources.ItemDurabilityChangedPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("f121286f-2e43-4a66-8f34-5dbe69304e1e")]
public class ItemDurabilityChangedPlugIn : IItemDurabilityChangedPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemDurabilityChangedPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ItemDurabilityChangedPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ItemDurabilityChangedAsync(Item item, bool afterConsumption)
    {
        await this._player.Connection.SendItemDurabilityChangedAsync(item.ItemSlot, item.Durability(), afterConsumption).ConfigureAwait(false);
    }
}