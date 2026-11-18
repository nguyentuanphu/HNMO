// <copyright file="HnmoCatchupCapRequestHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.CatchupCap;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Packet handler for <see cref="HnmoCatchupCapRequest"/> (<c>F5 3C</c>).
/// </summary>
[PlugIn]
[Display(Name = "HNMO Catch-up Cap Request Handler", Description = "Replies with today's Catch-up Level/Reset Cap state.")]
[Guid("9A4B2E5C-1D6F-43A8-B7E2-5F0C1A3D9E41")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class HnmoCatchupCapRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => HnmoCatchupCapRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null || CatchupCapPlugIn.Get(player.GameContext) is not { } plugIn)
        {
            return;
        }

        await plugIn.SendStateAsync(player, requested: true).ConfigureAwait(false);
    }
}
