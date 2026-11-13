// <copyright file="DropItemHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Items;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Items;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Pathfinding;
using HNMO.PlugIns;

/// <summary>
/// Handler for drop item packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.DropItemHandlerPlugIn_Name), Description = nameof(PlugInResources.DropItemHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("b79bc453-74a0-4eea-8bc3-014d737aaa88")]
internal class DropItemHandlerPlugIn : IPacketHandlerPlugIn
{
    private readonly DropItemAction _dropAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => DropItemRequest.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        DropItemRequest message = packet;
        await this._dropAction.DropItemAsync(player, message.ItemSlot, new Point(message.TargetX, message.TargetY)).ConfigureAwait(false);
    }
}