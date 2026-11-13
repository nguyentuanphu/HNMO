// <copyright file="TradeMoneyHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Trade;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Trade;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Handles the trade money packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.TradeMoneyHandlerPlugIn_Name), Description = nameof(PlugInResources.TradeMoneyHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("3c18f0ca-4ad8-4e07-a111-0acbe81256ca")]
internal class TradeMoneyHandlerPlugIn : IPacketHandlerPlugIn
{
    private readonly TradeMoneyAction _tradeAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => SetTradeMoney.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        SetTradeMoney message = packet;
        await this._tradeAction.TradeMoneyAsync(player, message.Amount).ConfigureAwait(false);
    }
}