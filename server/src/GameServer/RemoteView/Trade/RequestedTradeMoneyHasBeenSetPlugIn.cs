// <copyright file="RequestedTradeMoneyHasBeenSetPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Trade;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views.Trade;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IRequestedTradeMoneyHasBeenSetPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.RequestedTradeMoneyHasBeenSetPlugIn_Name), Description = nameof(PlugInResources.RequestedTradeMoneyHasBeenSetPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("d07bc23a-e4ec-462b-96d6-b2da739664d0")]
public class RequestedTradeMoneyHasBeenSetPlugIn : IRequestedTradeMoneyHasBeenSetPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestedTradeMoneyHasBeenSetPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public RequestedTradeMoneyHasBeenSetPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public ValueTask RequestedTradeMoneyHasBeenSetAsync()
    {
        return this._player.Connection.SendTradeMoneySetResponseAsync();
    }
}