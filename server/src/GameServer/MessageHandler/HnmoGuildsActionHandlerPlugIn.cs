// <copyright file="HnmoGuildsActionHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.Guilds;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Handler for the buttons of HNMO's guild levels panel (<see cref="HnmoGuildsAction"/>): taking the waiting rewards.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoGuildsActionHandlerPlugIn_Name), Description = nameof(PlugInResources.HnmoGuildsActionHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("2F6B8D13-7A40-4C59-A1E2-9D3C5B7F0A64")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class HnmoGuildsActionHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => HnmoGuildsAction.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null || packet.Length < 5 || GuildsPlugIn.Get(player.GameContext) is not { } guilds)
        {
            return;
        }

        HnmoGuildsAction message = packet;
        if (message.Action == HnmoGuildsAction.HnmoGuildsActionType.ClaimRewards)
        {
            await guilds.ClaimAsync(player).ConfigureAwait(false);
        }
        else
        {
            await guilds.SendStateAsync(player, true).ConfigureAwait(false);
        }
    }
}
