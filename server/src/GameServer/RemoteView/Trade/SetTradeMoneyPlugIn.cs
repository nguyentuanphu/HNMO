// <copyright file="SetTradeMoneyPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Trade;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views.Trade;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="ISetTradeMoneyPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.SetTradeMoneyPlugIn_Name), Description = nameof(PlugInResources.SetTradeMoneyPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("ca421b33-4ac2-4496-a8a3-11b740cded8c")]
public class SetTradeMoneyPlugIn : ISetTradeMoneyPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetTradeMoneyPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public SetTradeMoneyPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public ValueTask SetTradeMoneyAsync(uint moneyAmount)
    {
        return this._player.Connection.SendTradeMoneyUpdateAsync(moneyAmount);
    }
}