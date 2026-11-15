// <copyright file="HnmoCommunityActionHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.Community;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Handler for the buttons of HNMO's community window (<see cref="HnmoCommunityAction"/>): the Discord and Zalo coins,
/// an event's code and the waiting rewards.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoCommunityActionHandlerPlugIn_Name), Description = nameof(PlugInResources.HnmoCommunityActionHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("71C5B2E8-0D94-4A3F-8E27-B6F1A9C3D05E")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class HnmoCommunityActionHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => HnmoCommunityAction.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null || packet.Length < 5 || CommunityPlugIn.Get(player.GameContext) is not { } community)
        {
            return;
        }

        HnmoCommunityAction message = packet;
        var argument = packet.Length > 5 ? message.Argument.Trim() : string.Empty;
        switch (message.Action)
        {
            case HnmoCommunityAction.HnmoCommunityActionType.ClaimDiscord:
                await community.ClaimDiscordAsync(player).ConfigureAwait(false);
                break;
            case HnmoCommunityAction.HnmoCommunityActionType.ClaimZalo:
                await community.RequestZaloAsync(player, argument).ConfigureAwait(false);
                break;
            case HnmoCommunityAction.HnmoCommunityActionType.EnterCode:
                await community.EnterCodeAsync(player, argument).ConfigureAwait(false);
                break;
            case HnmoCommunityAction.HnmoCommunityActionType.ClaimRewards:
                await community.ClaimAsync(player).ConfigureAwait(false);
                break;
            default:
                await community.SendStateAsync(player, true).ConfigureAwait(false);
                break;
        }
    }
}
