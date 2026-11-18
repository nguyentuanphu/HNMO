// <copyright file="HnmoMountChainCommandHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.MountChain;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Packet handler for <see cref="HnmoMountChainCommand"/> (<c>F5 42</c>).
/// </summary>
[PlugIn]
[Display(Name = "HNMO Mount Chain Command Handler", Description = "Handles Mount Chain craft/upgrade, duplicate salvage, and summon selection.")]
[Guid("5B1D4A82-7E93-46F1-92A0-3C7E8D4B1954")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class HnmoMountChainCommandHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => HnmoMountChainCommand.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null || MountChainPlugIn.Get(player.GameContext) is not { } plugIn)
        {
            return;
        }

        HnmoMountChainCommand cmd = packet;
        var tierId = cmd.Id ?? string.Empty;
        switch (cmd.Action)
        {
            case 1:
                await plugIn.CraftTierAsync(player, tierId).ConfigureAwait(false);
                break;
            case 2:
                await plugIn.ActivateTierAsync(player, tierId).ConfigureAwait(false);
                break;
            case 3:
                await plugIn.SalvageDuplicateAsync(player, tierId).ConfigureAwait(false);
                break;
            default:
                await plugIn.SendStateAsync(player, requested: true).ConfigureAwait(false);
                break;
        }
    }
}
