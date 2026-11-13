// <copyright file="TradeAcceptHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Trade;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Trade;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Packet Handler which is called when a trade request gets answered by the player.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.TradeAcceptHandlerPlugIn_Name), Description = nameof(PlugInResources.TradeAcceptHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("79014c54-17a3-4e5e-85be-3e9c6051dbef")]
internal class TradeAcceptHandlerPlugIn : IPacketHandlerPlugIn
{
    private readonly TradeAcceptAction _acceptAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => TradeRequestResponse.Code;

    /// <inheritdoc/>
    /// <summary>The packet looks like: 0xC1, 0x04, 0x37, 0x01.</summary>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        TradeRequestResponse message = packet;
        await this._acceptAction.HandleTradeAcceptAsync(player, message.TradeAccepted).ConfigureAwait(false);
    }
}