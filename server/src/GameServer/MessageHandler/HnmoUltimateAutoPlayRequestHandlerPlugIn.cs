// <copyright file="HnmoUltimateAutoPlayRequestHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.UltimateAutoPlay;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Packet handler for <see cref="HnmoUltimateAutoPlayRequest"/> (<c>F5 56</c>).
/// </summary>
[PlugIn]
[Display(Name = "HNMO Ultimate Autoplay Request Handler", Description = "Replies with the player's Ultimate Autoplay Free vs VIP policy.")]
[Guid("B8D3C901-4E7A-41F5-8C62-3A9E5B1D7F22")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class HnmoUltimateAutoPlayRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => HnmoUltimateAutoPlayRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null)
        {
            return;
        }

        await UltimateAutoPlayPlugIn.SendPolicyAsync(player).ConfigureAwait(false);
    }
}
