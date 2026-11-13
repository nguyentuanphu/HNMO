// <copyright file="TradeCancelHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Trade;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Trade;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Handles the trade cancel packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.TradeCancelHandlerPlugIn_Name), Description = nameof(PlugInResources.TradeCancelHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("13c7ba03-0ec2-4f41-bc0a-30fb9a035240")]
internal class TradeCancelHandlerPlugIn : IPacketHandlerPlugIn
{
    private readonly TradeCancelAction _cancelHandler = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => TradeCancel.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        await this._cancelHandler.CancelTradeAsync(player).ConfigureAwait(false);
    }
}