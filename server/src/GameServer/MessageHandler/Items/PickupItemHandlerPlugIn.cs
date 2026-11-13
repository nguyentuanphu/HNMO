// <copyright file="PickupItemHandlerPlugIn.cs" company="HNMO">
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
/// Handler for item pickup packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.PickupItemHandlerPlugIn_Name), Description = nameof(PlugInResources.PickupItemHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("8bcb9d85-95ae-4611-ae64-e9cc801ec647")]
[MinimumClient(0, 97, ClientLanguage.Invariant)]
internal class PickupItemHandlerPlugIn : IPacketHandlerPlugIn
{
    private readonly PickupItemAction _pickupAction = new();

    /// <inheritdoc />
    public bool IsEncryptionExpected => true;

    /// <inheritdoc/>
    public byte Key => PickupItemRequest.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        PickupItemRequest message = packet;
        await this._pickupAction.PickupItemAsync(player, message.ItemId).ConfigureAwait(false);
    }
}