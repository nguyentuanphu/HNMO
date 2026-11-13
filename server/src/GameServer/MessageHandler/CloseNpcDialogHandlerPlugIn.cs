// <copyright file="CloseNpcDialogHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Packet handler for close npc packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.CloseNpcDialogHandlerPlugIn_Name), Description = nameof(PlugInResources.CloseNpcDialogHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("ecb920e3-eca7-4f40-a453-bdee67e1dabf")]
internal class CloseNpcDialogHandlerPlugIn : IPacketHandlerPlugIn
{
    private readonly CloseNpcDialogAction _closeNpcDialogAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => CloseNpcRequest.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        await this._closeNpcDialogAction.CloseNpcDialogAsync(player).ConfigureAwait(false);
    }
}