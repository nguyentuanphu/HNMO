// <copyright file="HnmoGiftCodesActionHandlerPlugIn.cs" company="HNMO">
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
/// Handler for the buttons of HNMO's gift code window (<see cref="HnmoGiftCodesAction"/>): enter a code, take the
/// rewards which wait for room in the bag.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoGiftCodesActionHandlerPlugIn_Name), Description = nameof(PlugInResources.HnmoGiftCodesActionHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("2C9A7E14-B635-4D8F-91A0-E4F3B6C28D57")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class HnmoGiftCodesActionHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => HnmoGiftCodesAction.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null || packet.Length < 5 || GiftCodesPlugIn.Get(player.GameContext) is not { } giftCodes)
        {
            return;
        }

        HnmoGiftCodesAction message = packet;
        switch (message.Action)
        {
            case HnmoGiftCodesAction.HnmoGiftCodesActionType.Redeem:
                await giftCodes.RedeemAsync(player, packet.Length > 5 ? message.Argument : string.Empty, true).ConfigureAwait(false);
                break;
            case HnmoGiftCodesAction.HnmoGiftCodesActionType.ClaimRewards:
                await giftCodes.ClaimAsync(player).ConfigureAwait(false);
                break;
            default:
                await giftCodes.SendStateAsync(player, true).ConfigureAwait(false);
                break;
        }
    }
}
