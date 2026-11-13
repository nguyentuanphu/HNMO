// <copyright file="PickupItemHandlerPlugIn075.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Items;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Items;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Handler for item pickup packets for client version 0.75.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.PickupItemHandlerPlugIn075_Name), Description = nameof(PlugInResources.PickupItemHandlerPlugIn075_Description), ResourceType = typeof(PlugInResources))]
[Guid("185512C3-F5B7-489F-A1C1-DF07146560A4")]
[MinimumClient(0, 75, ClientLanguage.Invariant)]
internal class PickupItemHandlerPlugIn075 : IPacketHandlerPlugIn
{
    private readonly PickupItemAction _pickupAction = new();

    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => PickupItemRequest.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        PickupItemRequest075 message = packet;
        await this._pickupAction.PickupItemAsync(player, message.ItemId).ConfigureAwait(false);
    }
}