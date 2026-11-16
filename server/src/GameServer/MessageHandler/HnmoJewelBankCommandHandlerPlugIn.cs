// <copyright file="HnmoJewelBankCommandHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.JewelBank;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Packet handler for <see cref="HnmoJewelBankCommand"/> (<c>F5 3A</c>).
/// </summary>
[PlugIn]
[Display(Name = "HNMO Jewel Bank Handler", Description = "Handles Jewel Bank deposit, withdraw, and master skill clear packets from the client.")]
[Guid("3C8A9F42-5E1B-47D0-9A63-11D2C87B40E5")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class HnmoJewelBankCommandHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => HnmoJewelBankCommand.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null || packet.Length < 11)
        {
            return;
        }

        if (JewelBankPlugIn.Get(player.GameContext) is not { } plugIn)
        {
            return;
        }

        HnmoJewelBankCommand cmd = packet;
        var action = cmd.Action;
        var amount = unchecked((int)cmd.Amount);
        var key = (cmd.Key ?? string.Empty).Trim();

        switch (action)
        {
            case 2:
                await plugIn.DepositAsync(player, cmd.Slot, depositAll: false).ConfigureAwait(false);
                break;
            case 3:
                await plugIn.DepositAsync(player, slot: 0, depositAll: true).ConfigureAwait(false);
                break;
            case 4:
                await plugIn.WithdrawAsync(player, key, amount).ConfigureAwait(false);
                break;
            case 5:
                await plugIn.ClearMasterSkillsAsync(player, fromPanel: true).ConfigureAwait(false);
                break;
            default:
                await plugIn.SendStateAsync(player, requested: true).ConfigureAwait(false);
                break;
        }
    }
}
