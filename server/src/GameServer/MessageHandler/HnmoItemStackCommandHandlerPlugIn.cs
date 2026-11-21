// <copyright file="HnmoItemStackCommandHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.ItemStacks;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>Handles <see cref="HnmoItemStackCommand"/> (<c>F5 50</c>): the stack kinds, splitting a stack, unlocking a kind.</summary>
[PlugIn]
[Display(Name = "HNMO item stacks handler", Description = "Answers the stack kinds, splits stacks and unlocks stack kinds with coins.")]
[Guid("9B26E4A1-7C3D-4E58-A0F9-2D6B8C1E5A47")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal sealed class HnmoItemStackCommandHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => HnmoItemStackCommand.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null)
        {
            return;
        }

        var command = new HnmoItemStackCommand(packet);
        if (ItemStackPlugIn.Get(player.GameContext) is not { } plugIn)
        {
            await player.InvokeViewPlugInAsync<IHnmoItemStackViewPlugIn>(p => p.ShowItemStacksAsync("{\"enabled\":false}")).ConfigureAwait(false);
            return;
        }

        bool? success = null;
        string? message = null;
        switch (command.Action)
        {
            case 1:
                (var split, message) = await plugIn.SplitAsync(player, command.Storage == 1, command.Slot, command.Count).ConfigureAwait(false);
                success = split;
                break;
            case 2:
                (var unlocked, message) = await plugIn.UnlockAsync(player, command.Kind).ConfigureAwait(false);
                success = unlocked;
                break;
        }

        await player.InvokeViewPlugInAsync<IHnmoItemStackViewPlugIn>(p => p.ShowItemStacksAsync(plugIn.BuildStateJson(player, success, message))).ConfigureAwait(false);
    }
}
