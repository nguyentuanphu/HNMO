// <copyright file="CashShopItemBuyRequestHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.CashShop;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.CashShop;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Packet handler for cash shop buy requests (0xD2, 0x03 identifier).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.CashShopItemBuyRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.CashShopItemBuyRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("89EB6F90-0CB9-4F27-957A-B6D158D01660")]
[BelongsToGroup(CashShopGroupHandlerPlugIn.GroupKey)]
internal class CashShopItemBuyRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly CashShopActions _cashShopActions = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => CashShopItemBuyRequest.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        CashShopItemBuyRequest message = packet;
        await this._cashShopActions.BuyAsync(
            player,
            (int)message.PackageMainIndex,
            (int)message.ProductMainIndex,
            message.CoinIndex.ToCashShopCoinType()).ConfigureAwait(false);
    }
}
