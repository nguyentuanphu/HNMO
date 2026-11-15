// <copyright file="HnmoCommunityRequestHandlerPlugIn.cs" company="HNMO">
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
/// Handler for the request of HNMO's community state (<see cref="HnmoCommunityRequest"/>).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoCommunityRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.HnmoCommunityRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("E4A07C93-8B15-4D2E-9F61-3C8B5D0A7E24")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class HnmoCommunityRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => HnmoCommunityRequest.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null || CommunityPlugIn.Get(player.GameContext) is not { } community)
        {
            return;
        }

        await community.SendStateAsync(player, true).ConfigureAwait(false);
    }
}
