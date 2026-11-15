// <copyright file="HnmoWorldBossesActionHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.WorldBosses;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Handler for the buttons of HNMO's world boss window (<see cref="HnmoWorldBossesAction"/>): taking the waiting rewards.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoWorldBossesActionHandlerPlugIn_Name), Description = nameof(PlugInResources.HnmoWorldBossesActionHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("E4A07B92-61C8-4D3F-8B25-0F9D3A6C71E4")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class HnmoWorldBossesActionHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => HnmoWorldBossesAction.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null || packet.Length < 5 || WorldBossesPlugIn.Get(player.GameContext) is not { } bosses)
        {
            return;
        }

        HnmoWorldBossesAction message = packet;
        if (message.Action == HnmoWorldBossesAction.HnmoWorldBossesActionType.ClaimRewards)
        {
            await bosses.ClaimAsync(player).ConfigureAwait(false);
        }
        else
        {
            await bosses.SendStateAsync(player, true).ConfigureAwait(false);
        }
    }
}
