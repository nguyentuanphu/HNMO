// <copyright file="PlayerShopClosePacketHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.PlayerShop;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.PlayerStore;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Packet handler which opens the player shop (3F 03).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.PlayerShopClosePacketHandlerPlugIn_Name), Description = nameof(PlugInResources.PlayerShopClosePacketHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("3DD40F3A-37FA-4694-80FB-BF71B454D98E")]
[BelongsToGroup(StoreHandlerGroupPlugIn.GroupKey)]
internal class PlayerShopClosePacketHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly CloseStoreAction _closeStoreAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => true;

    /// <inheritdoc/>
    public byte Key => PlayerShopClose.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        await this._closeStoreAction.CloseStoreAsync(player).ConfigureAwait(false);
    }
}