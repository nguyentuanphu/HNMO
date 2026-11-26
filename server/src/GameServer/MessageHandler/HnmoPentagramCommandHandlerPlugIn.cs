// <copyright file="HnmoPentagramCommandHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.SeasonNineSystems;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Packet handler for <see cref="HnmoPentagramCommand"/> (<c>F5 5B</c>): the Season 9 pentagram and errtel actions.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Pentagram Handler", Description = "Handles the Season 9 pentagram and errtel packets from the client.")]
[Guid("8E2D6A47-1C93-4B5F-A07E-3F9B5C2D1E84")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class HnmoPentagramCommandHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => HnmoPentagramCommand.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null || packet.Length < HnmoPentagramCommand.Length)
        {
            return;
        }

        HnmoPentagramCommand command = packet;
        PentagramActionResult? result = command.Action switch
        {
            2 => await PentagramActions.EquipAsync(player, command.Slot).ConfigureAwait(false),
            3 => await PentagramActions.UnequipAsync(player).ConfigureAwait(false),
            4 => await PentagramActions.AttachErrtelAsync(player, command.Slot, command.Index).ConfigureAwait(false),
            5 => await PentagramActions.RemoveErrtelAsync(player, command.Index).ConfigureAwait(false),
            6 => await PentagramActions.LevelUpErrtelAsync(player, command.Slot).ConfigureAwait(false),
            7 => await PentagramActions.RankUpErrtelAsync(player, command.Slot, command.Index).ConfigureAwait(false),
            _ => null,
        };

        await player.InvokeViewPlugInAsync<IHnmoPentagramViewPlugIn>(p => p.ShowPentagramAsync(PentagramStateWriter.Write(player, result))).ConfigureAwait(false);
    }
}
