// <copyright file="ShowMoneyDrop075.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.World;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views.World;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.Pathfinding;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IShowDroppedItemsPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowMoneyDrop075_Name), Description = nameof(PlugInResources.ShowMoneyDrop075_Description), ResourceType = typeof(PlugInResources))]
[Guid("2C00F283-3229-48A8-A974-3DE0C543DC17")]
[MaximumClient(0, 89, ClientLanguage.Invariant)]
public class ShowMoneyDrop075 : IShowMoneyDropPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowMoneyDrop075"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowMoneyDrop075(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public ValueTask ShowMoneyAsync(ushort itemId, bool isFreshDrop, uint amount, Point point)
    {
        return this._player.Connection.SendMoneyDropped075Async(itemId, isFreshDrop, point.X, point.Y, amount);
    }
}