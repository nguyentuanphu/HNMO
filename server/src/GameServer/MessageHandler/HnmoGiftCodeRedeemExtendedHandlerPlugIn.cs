// <copyright file="HnmoGiftCodeRedeemExtendedHandlerPlugIn.cs" company="HNMO">
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
/// Handles device-aware gift code redemption without changing legacy clients.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoGiftCodesActionHandlerPlugIn_Name), Description = nameof(PlugInResources.HnmoGiftCodesActionHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("0DF19872-B729-4A4D-9D24-01C6693D2D7A")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class HnmoGiftCodeRedeemExtendedHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => HnmoGiftCodeRedeemExtended.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null || packet.Length != HnmoGiftCodeRedeemExtended.Length
            || GiftCodesPlugIn.Get(player.GameContext) is not { } giftCodes)
        {
            return;
        }

        HnmoGiftCodeRedeemExtended message = packet;
        await giftCodes.RedeemAsync(player, message.GiftCode, true, message.DeviceId).ConfigureAwait(false);
    }
}
