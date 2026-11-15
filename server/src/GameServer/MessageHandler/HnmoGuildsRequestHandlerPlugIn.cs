// <copyright file="HnmoGuildsRequestHandlerPlugIn.cs" company="HNMO">
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
/// Handler for the request of HNMO's guild levels (<see cref="HnmoGuildsRequest"/>).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoGuildsRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.HnmoGuildsRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("D7E40A6B-1C93-4F25-8B7E-2A5C9F0D3E81")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class HnmoGuildsRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => HnmoGuildsRequest.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null || GuildsPlugIn.Get(player.GameContext) is not { } guilds)
        {
            return;
        }

        await guilds.SendStateAsync(player, true).ConfigureAwait(false);
    }
}
