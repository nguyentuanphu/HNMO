// <copyright file="ShowTradeRequestPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Trade;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.Views.Trade;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IShowTradeRequestPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowTradeRequestPlugIn_Name), Description = nameof(PlugInResources.ShowTradeRequestPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("2e6e8c0e-8220-46e3-931a-630d596178ca")]
public class ShowTradeRequestPlugIn : IShowTradeRequestPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowTradeRequestPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowTradeRequestPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowTradeRequestAsync(ITrader requester)
    {
        await this._player.Connection.SendTradeRequestAsync(requester.Name).ConfigureAwait(false);
    }
}