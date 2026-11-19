// <copyright file="HnmoVaultPageCommandHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.GameplayRules;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Handles <see cref="HnmoVaultPageCommand"/> (<c>F5 4E</c>): the vault window's page buttons and "one more page".
/// </summary>
[PlugIn]
[Display(Name = "HNMO vault pages handler", Description = "Switches vault pages and buys extra pages from the vault window.")]
[Guid("4A7C2E95-1D3B-4F68-9E0A-5B2C8D7F1E36")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal sealed class HnmoVaultPageCommandHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => HnmoVaultPageCommand.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null)
        {
            return;
        }

        var command = new HnmoVaultPageCommand(packet);
        if (MultiVaultPlugIn.Get(player.GameContext) is { } plugIn)
        {
            await plugIn.HandlePageCommandAsync(player, command.Action, command.Page).ConfigureAwait(false);
        }
        else
        {
            await MultiVaultPlugIn.SendDisabledAsync(player).ConfigureAwait(false);
        }
    }
}
