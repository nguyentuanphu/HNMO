// <copyright file="HnmoMuunCommandHandlerPlugIn.cs" company="HNMO">
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

/// <summary>Handles the Season 9 Muun commands (F5 2B) of the web client.</summary>
[PlugIn]
[Display(Name = "HNMO Muun Handler", Description = "Handles the Season 9 Muun commands from the client.")]
[Guid("A41D6E3B-2C87-4F09-9B5D-7E18C3A0F652")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class HnmoMuunCommandHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => HnmoMuunCommand.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null || packet.Length < HnmoMuunCommand.Length)
        {
            return;
        }

        if (SeasonNineSystemsPlugIn.Get(player.GameContext) is not { } plugIn)
        {
            // Turned off: answer anyway, or the window waits for a state that never comes.
            await player.InvokeViewPlugInAsync<IHnmoMuunViewPlugIn>(p => p.ShowMuunAsync(MuunCompanion.DisabledState, requested: true)).ConfigureAwait(false);
            return;
        }

        HnmoMuunCommand command = packet;
        var muuns = plugIn.Muuns;
        switch (command.Action)
        {
            case 2:
                await muuns.EquipAsync(player, command.Slot).ConfigureAwait(false);
                break;
            case 3:
                await muuns.UnequipAsync(player).ConfigureAwait(false);
                break;
            case 4:
                await muuns.EvolveAsync(player).ConfigureAwait(false);
                break;
            case 5:
                await muuns.ConvertAsync(player, command.Slot).ConfigureAwait(false);
                break;
            case 6:
                await muuns.LevelUpAsync(player).ConfigureAwait(false);
                break;
            default:
                await muuns.SendStateAsync(player, requested: true).ConfigureAwait(false);
                break;
        }
    }
}
