// <copyright file="HnmoMissionsRequestHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.GameLogic.PlugIns.Onboarding;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Handler for the request of HNMO's missions (<see cref="HnmoMissionsRequest"/>).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoMissionsRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.HnmoMissionsRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("4F8E2A61-B39C-4D75-A0E6-1B7C93D52E48")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class HnmoMissionsRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => HnmoMissionsRequest.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null || MissionsPlugIn.Get(player.GameContext) is not { } missions)
        {
            return;
        }

        await missions.SendBoardAsync(player, true).ConfigureAwait(false);

        // The missions window was opened (it asks then): a step of the new-player guide.
        if (OnboardingPlugIn.Get(player.GameContext) is { } onboarding)
        {
            await onboarding.MissionsOpenedAsync(player).ConfigureAwait(false);
        }
    }
}
