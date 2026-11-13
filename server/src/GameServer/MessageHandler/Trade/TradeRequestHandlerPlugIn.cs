// <copyright file="TradeRequestHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Trade;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Trade;
using HNMO.GameLogic.Properties;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;
using PlugInResources = HNMO.GameServer.Properties.PlugInResources;

/// <summary>
/// Handles the trade request packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.TradeRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.TradeRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("f2b8c4c0-2e9d-4f1f-8c42-76b0312e4021")]
internal class TradeRequestHandlerPlugIn : IPacketHandlerPlugIn
{
    private readonly TradeRequestAction _requestAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => true;

    /// <inheritdoc/>
    public byte Key => TradeRequest.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        TradeRequest message = packet;
        var partner = await player.GetObservingPlayerWithIdAsync(message.PlayerId).ConfigureAwait(false);
        if (partner is null)
        {
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.TradePartnerNotFound)).ConfigureAwait(false);
            return;
        }

        await this._requestAction.RequestTradeAsync(player, partner).ConfigureAwait(false);
    }
}