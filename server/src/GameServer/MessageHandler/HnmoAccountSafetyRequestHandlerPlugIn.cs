// <copyright file="HnmoAccountSafetyRequestHandlerPlugIn.cs" company="HNMO">
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
/// Handler for the request of HNMO's account safety state (<see cref="HnmoAccountSafetyRequest"/>).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoAccountSafetyRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.HnmoAccountSafetyRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("6D2F8A14-93B7-4E5C-A1D0-58E3C7B92F46")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class HnmoAccountSafetyRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => HnmoAccountSafetyRequest.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null || AccountSafetyPlugIn.Get(player.GameContext) is not { } safety)
        {
            return;
        }

        await safety.SendStateAsync(player, true).ConfigureAwait(false);
    }
}
