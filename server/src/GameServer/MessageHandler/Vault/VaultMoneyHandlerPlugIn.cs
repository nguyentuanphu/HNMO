// <copyright file="VaultMoneyHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Vault;

using System.ComponentModel;
using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.Properties;
using HNMO.GameLogic.Views.Vault;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;
using PlugInResources = HNMO.GameServer.Properties.PlugInResources;

/// <summary>
/// Handler for warehouse money packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.VaultMoneyHandlerPlugIn_Name), Description = nameof(PlugInResources.VaultMoneyHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("e365f3f2-55c8-4890-9f6b-26fd39822b71")]
internal class VaultMoneyHandlerPlugIn : IPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => VaultMoveMoneyRequest.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        VaultMoveMoneyRequest request = packet;
        switch (request.Direction)
        {
            case VaultMoveMoneyRequest.VaultMoneyMoveDirection.InventoryToVault:
                player.TryDepositVaultMoney(request.Amount);
                break;
            case VaultMoveMoneyRequest.VaultMoneyMoveDirection.VaultToInventory:
                if (player.IsVaultLocked)
                {
                    await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.TheVaultIsLocked)).ConfigureAwait(false);
                    break;
                }

                player.TryTakeVaultMoney(request.Amount);
                break;
            default:
                throw new InvalidEnumArgumentException($"The direction {request.Direction} is not a valid value.");
        }

        await player.InvokeViewPlugInAsync<IUpdateVaultMoneyPlugIn>(p => p.UpdateVaultMoneyAsync(true)).ConfigureAwait(false);
    }
}