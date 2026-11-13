// <copyright file="ItemSoldToNpcPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Inventory;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views.Inventory;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IItemSoldToNpcPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ItemSoldToNpcPlugIn_Name), Description = nameof(PlugInResources.ItemSoldToNpcPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("8372476a-7fb9-4f6e-a857-41c39c7d377c")]
public class ItemSoldToNpcPlugIn : IItemSoldToNpcPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemSoldToNpcPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ItemSoldToNpcPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ItemSoldToNpcAsync(bool success)
    {
        await this._player.Connection.SendNpcItemSellResultAsync(success, (uint)this._player.Money).ConfigureAwait(false);
    }
}