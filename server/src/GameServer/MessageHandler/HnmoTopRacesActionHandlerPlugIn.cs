// <copyright file="HnmoTopRacesActionHandlerPlugIn.cs" company="HNMO">
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
/// Handler for the buttons of HNMO's top races window (<see cref="HnmoTopRacesAction"/>): take the waiting prizes.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoTopRacesActionHandlerPlugIn_Name), Description = nameof(PlugInResources.HnmoTopRacesActionHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("1F8C5D93-2E47-4B6A-9D05-7A3C8E61B2F4")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class HnmoTopRacesActionHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => HnmoTopRacesAction.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null || packet.Length < 5 || TopRacesPlugIn.Get(player.GameContext) is not { } topRaces)
        {
            return;
        }

        HnmoTopRacesAction message = packet;
        if (message.Action == HnmoTopRacesAction.HnmoTopRacesActionType.ClaimRewards)
        {
            await topRaces.ClaimAsync(player).ConfigureAwait(false);
        }
        else
        {
            await topRaces.SendStateAsync(player, true).ConfigureAwait(false);
        }
    }
}
