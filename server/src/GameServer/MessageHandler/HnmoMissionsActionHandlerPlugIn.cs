// <copyright file="HnmoMissionsActionHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Handler for the buttons of HNMO's missions window (<see cref="HnmoMissionsAction"/>): claims and the invite code.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoMissionsActionHandlerPlugIn_Name), Description = nameof(PlugInResources.HnmoMissionsActionHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("C7B04E93-5A2D-4F18-86B3-E9D1A6F04C27")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class HnmoMissionsActionHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => HnmoMissionsAction.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null || packet.Length < 5 || MissionsPlugIn.Get(player.GameContext) is not { } missions)
        {
            return;
        }

        HnmoMissionsAction message = packet;
        var argument = packet.Length > 5 ? message.Argument : string.Empty;
        switch (message.Action)
        {
            case HnmoMissionsAction.HnmoMissionsActionType.ClaimMission:
                await missions.ClaimMissionAsync(player, argument).ConfigureAwait(false);
                break;
            case HnmoMissionsAction.HnmoMissionsActionType.ClaimStreak:
                await missions.ClaimStreakAsync(player).ConfigureAwait(false);
                break;
            case HnmoMissionsAction.HnmoMissionsActionType.ClaimCalendar:
                await missions.ClaimCalendarAsync(player).ConfigureAwait(false);
                break;
            case HnmoMissionsAction.HnmoMissionsActionType.EnterInvite:
                await missions.EnterInviteAsync(player, argument).ConfigureAwait(false);
                break;
            default:
                await missions.SendBoardAsync(player, true).ConfigureAwait(false);
                break;
        }
    }
}
