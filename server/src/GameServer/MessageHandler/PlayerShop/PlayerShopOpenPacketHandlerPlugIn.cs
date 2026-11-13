// <copyright file="PlayerShopOpenPacketHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.PlayerShop;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.PlayerStore;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Packet handler which opens the player shop (3F 02).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.PlayerShopOpenPacketHandlerPlugIn_Name), Description = nameof(PlugInResources.PlayerShopOpenPacketHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("6997E1D3-B722-40AE-8F71-A65BB4377529")]
[BelongsToGroup(StoreHandlerGroupPlugIn.GroupKey)]
internal class PlayerShopOpenPacketHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly OpenStoreAction _openStoreAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => true;

    /// <inheritdoc/>
    public byte Key => PlayerShopOpen.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        PlayerShopOpen message = packet;
        await this._openStoreAction.OpenStoreAsync(player, message.StoreName).ConfigureAwait(false);
    }
}