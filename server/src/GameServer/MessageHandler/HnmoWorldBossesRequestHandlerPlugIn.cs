// <copyright file="HnmoWorldBossesRequestHandlerPlugIn.cs" company="HNMO">
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
/// Handler for the request of HNMO's world bosses (<see cref="HnmoWorldBossesRequest"/>).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoWorldBossesRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.HnmoWorldBossesRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("9C1D7E35-2B84-4F60-A3E9-6D5B0C8F17A2")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class HnmoWorldBossesRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => HnmoWorldBossesRequest.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null || WorldBossesPlugIn.Get(player.GameContext) is not { } bosses)
        {
            return;
        }

        await bosses.SendStateAsync(player, true).ConfigureAwait(false);
    }
}
