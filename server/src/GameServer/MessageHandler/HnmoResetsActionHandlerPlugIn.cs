// <copyright file="HnmoResetsActionHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.ResetOptions;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Handler for the reset actions of the web game (<see cref="HnmoResetsAction"/>): reset, relife, the next reset's
/// needs, and the delegated reset switch.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoResetsActionHandlerPlugIn_Name), Description = nameof(PlugInResources.HnmoResetsActionHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("D17E4A93-2B6C-4F58-8A0E-6C95B3F2E174")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class HnmoResetsActionHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => HnmoResetsAction.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null || packet.Length < 5 || ResetOptionsPlugIn.Get(player.GameContext) is not { } resets)
        {
            return;
        }

        HnmoResetsAction message = packet;
        await resets.SendStateAsync(player, true).ConfigureAwait(false);
        switch (message.Action)
        {
            case HnmoResetsAction.HnmoResetsActionType.Reset:
                await resets.ResetAsync(player).ConfigureAwait(false);
                break;
            case HnmoResetsAction.HnmoResetsActionType.Relife:
                await resets.RelifeAsync(player).ConfigureAwait(false);
                break;
            case HnmoResetsAction.HnmoResetsActionType.DelegatedOn:
                await resets.SetDelegatedAsync(player, true, true).ConfigureAwait(false);
                break;
            case HnmoResetsAction.HnmoResetsActionType.DelegatedOff:
                await resets.SetDelegatedAsync(player, false, true).ConfigureAwait(false);
                break;
            case HnmoResetsAction.HnmoResetsActionType.Info:
                await resets.InfoAsync(player).ConfigureAwait(false);
                break;
        }
    }
}
