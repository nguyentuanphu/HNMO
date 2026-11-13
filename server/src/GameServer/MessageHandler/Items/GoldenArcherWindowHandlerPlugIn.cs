// <copyright file="GoldenArcherWindowHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Items;

using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Items.ItemRegistration;
using HNMO.GameLogic.Views.NPC;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Handler for the Golden Archer registration window.
/// </summary>
[PlugIn]
[Guid("F68AFB2B-CEE0-420E-89F2-30694045ED45")]
[MinimumClient(0, 90, ClientLanguage.Invariant)]
internal class GoldenArcherWindowHandlerPlugIn : IPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => EventChipRegistrationRequest.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        var action = new ItemRegistrationAction();
        var success = await action.RegisterAsync(player).ConfigureAwait(false);
        if (!success && player.OpenedNpc is { } npc)
        {
            await player.InvokeViewPlugInAsync<IItemRegistrationResultPlugIn>(
                p => p.RegistrationResultAsync(npc.Definition.Number, ItemRegistrationOperation.MissingItem)).ConfigureAwait(false);
        }
    }
}
