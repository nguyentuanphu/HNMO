// <copyright file="SetVaultPinPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Vault;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Vault;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Packet handler for vault pin set packets (0x83, 0x01 identifier).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.SetVaultPinPlugIn_Name), Description = nameof(PlugInResources.SetVaultPinPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("A4C4CD69-6E28-4088-B533-CD63589D3CCA")]
[BelongsToGroup(VaultLockGroupPlugIn.GroupKey)]
internal class SetVaultPinPlugIn : ISubPacketHandlerPlugIn
{
    private readonly SetVaultPinAction _setVaultPinAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => SetVaultPin.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        SetVaultPin message = packet;
        await this._setVaultPinAction.SetPinAsync(player, message.Pin.ToString(), message.Password).ConfigureAwait(false);
    }
}