// <copyright file="HnmoEscrowCommandHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using System.Text.Json;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.Escrow;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Handles <see cref="HnmoEscrowCommand"/> (<c>F5 4C</c>) packets from web clients.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Escrow Contracts handler", Description = "Processes escrow contract queries, creations, acceptances, cancellations, and claims.")]
[Guid("3C5D7E9F-2A4B-4C6D-8E1F-3B5C7D9E1F2A")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal sealed class HnmoEscrowCommandHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => HnmoEscrowCommand.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null)
        {
            return;
        }

        var cmd = new HnmoEscrowCommand(packet);
        if (EscrowContractsPlugIn.Get(player.GameContext) is not { } plugIn)
        {
            var disabledJson = JsonSerializer.Serialize(new { enabled = false });
            await player.InvokeViewPlugInAsync<IHnmoEscrowViewPlugIn>(p => p.ShowEscrowAsync(disabledJson, requested: true)).ConfigureAwait(false);
            return;
        }

        await plugIn.HandlePacketAsync(player, cmd.Action, cmd.Payload).ConfigureAwait(false);
    }
}
