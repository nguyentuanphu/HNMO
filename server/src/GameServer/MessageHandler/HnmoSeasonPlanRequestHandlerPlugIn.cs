// <copyright file="HnmoSeasonPlanRequestHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.SeasonPlan;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Packet handler for <see cref="HnmoSeasonPlanRequest"/> (<c>F5 3E</c>).
/// </summary>
[PlugIn]
[Display(Name = "HNMO Season Plan Request Handler", Description = "Replies with the current Season Plan timeline state.")]
[Guid("E23A5C48-6B7D-491F-B82E-4C109A5D7F32")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class HnmoSeasonPlanRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => HnmoSeasonPlanRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null || SeasonPlanPlugIn.Get(player.GameContext) is not { } plugIn)
        {
            return;
        }

        await plugIn.SendStateAsync(player, requested: true).ConfigureAwait(false);
    }
}
