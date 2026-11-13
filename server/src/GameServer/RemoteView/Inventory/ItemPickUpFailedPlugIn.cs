// <copyright file="ItemPickUpFailedPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Inventory;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views.Inventory;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IItemPickUpFailedPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ItemPickUpFailedPlugIn_Name), Description = nameof(PlugInResources.ItemPickUpFailedPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("f73a2cee-14bb-4404-a321-767f848e3571")]
public class ItemPickUpFailedPlugIn : IItemPickUpFailedPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemPickUpFailedPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ItemPickUpFailedPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ItemPickUpFailedAsync(ItemPickFailReason reason)
    {
        await this._player.Connection.SendItemPickUpRequestFailedAsync(reason.Convert()).ConfigureAwait(false);
    }
}