// <copyright file="HnmoMarriageCommandHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.Marriage;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Handles <see cref="HnmoMarriageCommand"/> packets (<c>F5 36</c>).
/// </summary>
[PlugIn]
[Display(Name = "HNMO Marriage handler", Description = "Handles marriage proposals, Devias ceremonies, couple titles, spouse warp, and divorces.")]
[Guid("69A8D3C2-4E71-40B5-9C64-28F5E0D9B7A1")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal sealed class HnmoMarriageCommandHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => HnmoMarriageCommand.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null)
        {
            return;
        }

        var command = new HnmoMarriageCommand(packet);
        var plugIn = MarriagePlugIn.Get(player.GameContext);
        if (plugIn is null)
        {
            if (command.Action == HnmoMarriageCommand.HnmoMarriageActionType.Request)
            {
                var offEngine = new MarriageEngine(new MarriageConfiguration(), MarriageStore.Open(null), enabled: false);
                var offJson = MarriageStateWriter.WritePlayerState(offEngine, player.SelectedCharacter.Name, bonusActiveNow: false, DateTime.UtcNow);
                await player.InvokeViewPlugInAsync<IHnmoMarriageViewPlugIn>(p => p.ShowMarriageAsync(offJson, requested: true)).ConfigureAwait(false);
            }

            return;
        }

        switch (command.Action)
        {
            case HnmoMarriageCommand.HnmoMarriageActionType.Request:
                await plugIn.SendStateAsync(player, requested: true).ConfigureAwait(false);
                break;
            case HnmoMarriageCommand.HnmoMarriageActionType.Propose:
                await plugIn.ProposeAsync(player, command.Argument).ConfigureAwait(false);
                break;
            case HnmoMarriageCommand.HnmoMarriageActionType.Accept:
                await plugIn.AcceptAsync(player).ConfigureAwait(false);
                break;
            case HnmoMarriageCommand.HnmoMarriageActionType.Decline:
                await plugIn.DeclineAsync(player).ConfigureAwait(false);
                break;
            case HnmoMarriageCommand.HnmoMarriageActionType.Divorce:
                await plugIn.DivorceAsync(player).ConfigureAwait(false);
                break;
            case HnmoMarriageCommand.HnmoMarriageActionType.SetTitle:
                await plugIn.SetTitleAsync(player, command.Argument).ConfigureAwait(false);
                break;
            case HnmoMarriageCommand.HnmoMarriageActionType.TeleportToSpouse:
                await plugIn.TeleportToSpouseAsync(player).ConfigureAwait(false);
                break;
        }
    }
}
