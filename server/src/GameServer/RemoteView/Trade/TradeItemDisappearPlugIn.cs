// <copyright file="TradeItemDisappearPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Trade;

using System.Runtime.InteropServices;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.Views.Trade;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="ITradeItemDisappearPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.TradeItemDisappearPlugIn_Name), Description = nameof(PlugInResources.TradeItemDisappearPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("5ac9569b-3c5a-468e-bf48-1e67f81281c5")]
public class TradeItemDisappearPlugIn : ITradeItemDisappearPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="TradeItemDisappearPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public TradeItemDisappearPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public ValueTask TradeItemDisappearAsync(byte slot, Item item)
    {
        return this._player.Connection.SendTradeItemRemovedAsync(slot);
    }
}