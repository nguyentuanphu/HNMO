// <copyright file="ItemMoveExtendedHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Items;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Items;
using HNMO.GameServer.RemoteView.Inventory;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Packet handler for extended item move packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ItemMoveExtendedHandlerPlugIn_Name), Description = nameof(PlugInResources.ItemMoveExtendedHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("F68AFB2B-CEE0-420E-89F2-30694045ED46")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
internal class ItemMoveExtendedHandlerPlugIn : IPacketHandlerPlugIn
{
    private readonly MoveItemAction _moveAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => ItemMoveRequest.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        ItemMoveRequestExtended message = packet;

        // we don't transmit the item binary data anymore in this extended message.
        await this._moveAction.MoveItemAsync(player, message.FromSlot, message.FromStorage.Convert(), message.ToSlot, message.ToStorage.Convert()).ConfigureAwait(false);
    }
}