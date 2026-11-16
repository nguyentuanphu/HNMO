// <copyright file="HnmoTopUpMilestonesCommandHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.TopUpMilestones;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Handles <see cref="HnmoTopUpMilestonesCommand"/> packets (<c>F5 30</c>).
/// </summary>
[PlugIn]
[Display(Name = "HNMO Top-up milestones handler", Description = "Handles HNMO top-up milestone requests, claims, and owed item deliveries.")]
[Guid("41C5E9F0-8A23-41D7-B6E2-1F4089A5C733")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class HnmoTopUpMilestonesCommandHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => HnmoTopUpMilestonesCommand.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null)
        {
            return;
        }

        if (TopUpMilestonesPlugIn.Get(player.GameContext) is not { } plugIn)
        {
            return;
        }

        var command = new HnmoTopUpMilestonesCommand(packet);
        switch (command.Action)
        {
            case HnmoTopUpMilestonesCommand.HnmoTopUpMilestonesActionType.Request:
                await plugIn.SendStateAsync(player, true).ConfigureAwait(false);
                break;
            case HnmoTopUpMilestonesCommand.HnmoTopUpMilestonesActionType.Claim:
                await plugIn.ClaimAsync(player, command.Argument).ConfigureAwait(false);
                break;
            case HnmoTopUpMilestonesCommand.HnmoTopUpMilestonesActionType.TakeOwed:
                await plugIn.TakeOwedAsync(player).ConfigureAwait(false);
                break;
        }
    }
}
