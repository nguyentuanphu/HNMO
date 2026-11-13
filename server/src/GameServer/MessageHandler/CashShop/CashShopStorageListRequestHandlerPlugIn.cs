// <copyright file="CashShopStorageListRequestHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.CashShop;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.CashShop;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Packet handler for cash shop storage list requests (0xD2, 0x05 identifier).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.CashShopStorageListRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.CashShopStorageListRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("F4068073-8FA2-4F02-B80C-2FFF76B384FA")]
[BelongsToGroup(CashShopGroupHandlerPlugIn.GroupKey)]
internal class CashShopStorageListRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <summary>
    /// The inventory type of the gift storage, the ASCII character 'G'.
    /// The client requests the normal storage with 'S'.
    /// </summary>
    private const byte GiftStorageInventoryType = (byte)'G';

    private readonly CashShopActions _cashShopActions = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => CashShopStorageListRequest.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        CashShopStorageListRequest message = packet;
        var pageNumber = (int)Math.Min(message.PageIndex, int.MaxValue);
        var isGiftStorage = message.InventoryType == GiftStorageInventoryType;
        await this._cashShopActions.ShowStorageAsync(player, pageNumber, isGiftStorage).ConfigureAwait(false);
    }
}
