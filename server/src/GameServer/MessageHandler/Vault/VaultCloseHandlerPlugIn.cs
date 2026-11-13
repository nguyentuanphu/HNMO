// <copyright file="VaultCloseHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Vault;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Handler for warehouse close packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.VaultCloseHandlerPlugIn_Name), Description = nameof(PlugInResources.VaultCloseHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("7859931f-3341-4bd7-91ad-1b0b03f11198")]
internal class VaultCloseHandlerPlugIn : IPacketHandlerPlugIn
{
    private readonly CloseNpcDialogAction _closeDialogAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => VaultClosed.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        await this._closeDialogAction.CloseNpcDialogAsync(player).ConfigureAwait(false);
    }
}