// <copyright file="HnmoOnboardingRequestHandlerPlugIn.cs" company="HNMO">
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
/// Handler for the request of HNMO's new-player guide (<see cref="HnmoOnboardingRequest"/>).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoOnboardingRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.HnmoOnboardingRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("C51A7D04-3E92-4B68-8F27-1D6E9B40A5C3")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class HnmoOnboardingRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => HnmoOnboardingRequest.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null || OnboardingPlugIn.Get(player.GameContext) is not { } onboarding)
        {
            return;
        }

        await onboarding.SendStateAsync(player, true).ConfigureAwait(false);
    }
}
