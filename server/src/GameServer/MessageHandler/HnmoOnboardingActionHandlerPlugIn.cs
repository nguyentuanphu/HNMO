// <copyright file="HnmoOnboardingActionHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.Onboarding;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Handler for the buttons of HNMO's guide panel (<see cref="HnmoOnboardingAction"/>): skipping the guide or an optional
/// step, and taking the rewards which waited for room in the bag.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoOnboardingActionHandlerPlugIn_Name), Description = nameof(PlugInResources.HnmoOnboardingActionHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("2E8D6F17-94A3-4C05-B7E6-58A1C3D90F24")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class HnmoOnboardingActionHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => HnmoOnboardingAction.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null || packet.Length < 5 || OnboardingPlugIn.Get(player.GameContext) is not { } onboarding)
        {
            return;
        }

        HnmoOnboardingAction message = packet;
        switch (message.Action)
        {
            case HnmoOnboardingAction.HnmoOnboardingActionType.SkipGuide:
                await onboarding.SkipAsync(player).ConfigureAwait(false);
                break;
            case HnmoOnboardingAction.HnmoOnboardingActionType.SkipStep:
                await onboarding.SkipStepAsync(player).ConfigureAwait(false);
                break;
            case HnmoOnboardingAction.HnmoOnboardingActionType.ClaimRewards:
                await onboarding.ClaimAsync(player).ConfigureAwait(false);
                break;
            default:
                await onboarding.SendStateAsync(player, true).ConfigureAwait(false);
                break;
        }
    }
}
