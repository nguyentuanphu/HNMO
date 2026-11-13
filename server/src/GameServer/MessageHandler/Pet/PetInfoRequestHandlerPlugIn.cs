// <copyright file="PetInfoRequestHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Pet;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Items;
using HNMO.GameServer;
using HNMO.GameServer.MessageHandler;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Handler for pet info request packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.PetInfoRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.PetInfoRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("DA535FF5-A23D-4C36-877F-73D1D811F146")]
[MinimumClient(0, 97, ClientLanguage.Invariant)]
internal class PetInfoRequestHandlerPlugIn : IPacketHandlerPlugIn
{
    private readonly PetInfoRequestAction _requestAction = new();

    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => PetInfoRequest.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        PetInfoRequest message = packet;
        await this._requestAction.RequestPetInfoAsync(player, message.ItemSlot, ConvertStorageLocation(message.Storage)).ConfigureAwait(false);
    }

    private static PetStorageLocation ConvertStorageLocation(StorageType location)
    {
        return location switch
        {
            StorageType.Crafting => PetStorageLocation.Crafting,
            StorageType.Inventory => PetStorageLocation.Inventory,
            StorageType.InventoryPetSlot => PetStorageLocation.InventoryPetSlot,
            StorageType.PersonalShop => PetStorageLocation.PersonalShop,
            StorageType.NpcShop => PetStorageLocation.NpcShop,
            StorageType.TradeOther => PetStorageLocation.TradeOther,
            StorageType.TradeOwn => PetStorageLocation.TradeOwn,
            StorageType.Vault => PetStorageLocation.Vault,
            _ => throw new ArgumentOutOfRangeException(nameof(location)),
        };
    }
}