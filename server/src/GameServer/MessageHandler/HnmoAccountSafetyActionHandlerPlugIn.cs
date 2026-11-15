// <copyright file="HnmoAccountSafetyActionHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.AccountSafety;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Handler for the account safety actions of the web game (<see cref="HnmoAccountSafetyAction"/>): enter the second
/// password, lock an item, ask an item's unlock.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoAccountSafetyActionHandlerPlugIn_Name), Description = nameof(PlugInResources.HnmoAccountSafetyActionHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("A3E95C72-1F48-4D06-B7C2-94D16E8F0A53")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class HnmoAccountSafetyActionHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => HnmoAccountSafetyAction.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null || packet.Length < HnmoAccountSafetyAction.Length || AccountSafetyPlugIn.Get(player.GameContext) is not { } safety)
        {
            return;
        }

        HnmoAccountSafetyAction message = packet;
        var pin = AccountSafetyEngine.PinOf(message.Pin);
        switch (message.Action)
        {
            case HnmoAccountSafetyAction.HnmoAccountSafetyActionType.EnterPin:
                await safety.EnterPinAsync(player, pin, true).ConfigureAwait(false);
                break;
            case HnmoAccountSafetyAction.HnmoAccountSafetyActionType.LockItem:
                await safety.LockItemAsync(player, message.Vault, message.Slot).ConfigureAwait(false);
                break;
            case HnmoAccountSafetyAction.HnmoAccountSafetyActionType.UnlockItem:
                await safety.UnlockItemAsync(player, message.Vault, message.Slot, pin).ConfigureAwait(false);
                break;
            default:
                await safety.SendStateAsync(player, true).ConfigureAwait(false);
                break;
        }
    }
}
