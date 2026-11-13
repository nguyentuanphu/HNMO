// <copyright file="ItemDropResultPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Inventory;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views.Inventory;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IItemDropResultPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ItemDropResultPlugIn_Name), Description = nameof(PlugInResources.ItemDropResultPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("377cd4cb-7334-4c74-a165-058e6bb46baf")]
public class ItemDropResultPlugIn : IItemDropResultPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemDropResultPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ItemDropResultPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ItemDropResultAsync(byte slot, bool success)
    {
        await this._player.Connection.SendItemDropResponseAsync(success, slot).ConfigureAwait(false);
    }
}