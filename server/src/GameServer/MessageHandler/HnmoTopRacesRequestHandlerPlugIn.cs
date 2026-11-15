// <copyright file="HnmoTopRacesRequestHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.TopRaces;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Handler for the request of HNMO's top races state (<see cref="HnmoTopRacesRequest"/>).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoTopRacesRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.HnmoTopRacesRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("8D3F6A21-C7B4-4E59-A0D2-6B9E1F37C584")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class HnmoTopRacesRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => HnmoTopRacesRequest.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null || TopRacesPlugIn.Get(player.GameContext) is not { } topRaces)
        {
            return;
        }

        await topRaces.SendStateAsync(player, true).ConfigureAwait(false);
    }
}
