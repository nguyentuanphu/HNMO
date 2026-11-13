// <copyright file="CraftingDialogCloseRequestHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Handler for chaos mix packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.CraftingDialogCloseRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.CraftingDialogCloseRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("1857513c-d09c-4e03-8bf4-f4ead19ea60f")]
internal class CraftingDialogCloseRequestHandlerPlugIn : IPacketHandlerPlugIn
{
    private readonly CloseNpcDialogAction _closeNpcDialogAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => CraftingDialogCloseRequest.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        await this._closeNpcDialogAction.CloseNpcDialogAsync(player).ConfigureAwait(false);
    }
}