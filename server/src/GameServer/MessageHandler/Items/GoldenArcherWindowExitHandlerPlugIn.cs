// <copyright file="GoldenArcherWindowExitHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Items;

using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions;
using HNMO.GameLogic.PlayerActions.Items.ItemRegistration;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Handler for Golden Archer exit dialog packets (0x97).
/// </summary>
[PlugIn]
[Guid("ED982BA5-39A0-449D-BB11-4BD31C77926A")]
[MinimumClient(0, 90, ClientLanguage.Invariant)]
internal class GoldenArcherWindowExitHandlerPlugIn : IPacketHandlerPlugIn
{
    private readonly CloseNpcDialogAction _closeNpcDialogAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => EventChipExitDialog.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        await this._closeNpcDialogAction.CloseNpcDialogAsync(player).ConfigureAwait(false);
    }
}
