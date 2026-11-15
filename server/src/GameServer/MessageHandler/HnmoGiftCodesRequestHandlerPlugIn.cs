// <copyright file="HnmoGiftCodesRequestHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Handler for the request of HNMO's gift codes state (<see cref="HnmoGiftCodesRequest"/>).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoGiftCodesRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.HnmoGiftCodesRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("B7C40E92-5D18-4A3F-8E61-29F0A7D53C84")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class HnmoGiftCodesRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => HnmoGiftCodesRequest.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null || GiftCodesPlugIn.Get(player.GameContext) is not { } giftCodes)
        {
            return;
        }

        await giftCodes.SendStateAsync(player, true).ConfigureAwait(false);
    }
}
