// <copyright file="HnmoResetsRequestHandlerPlugIn.cs" company="HNMO">
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
/// Handler for the request of HNMO's reset state (<see cref="HnmoResetsRequest"/>).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoResetsRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.HnmoResetsRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("3F6B9E20-81C4-4A7D-95E3-C2D07A41B858")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class HnmoResetsRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => HnmoResetsRequest.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null || ResetOptionsPlugIn.Get(player.GameContext) is not { } resets)
        {
            return;
        }

        await resets.SendStateAsync(player, true).ConfigureAwait(false);
    }
}
