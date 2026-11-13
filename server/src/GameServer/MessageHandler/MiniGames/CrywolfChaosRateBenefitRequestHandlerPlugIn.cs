// <copyright file="CrywolfChaosRateBenefitRequestHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.MiniGames;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.Crywolf;
using HNMO.GameServer.Properties;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Handler for 0xBD/0x09 — CrywolfChaosRateBenefitRequest, which the client sends when it opens a crafting dialog.
/// The server responds with the additional success rate of the crywolf event.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.CrywolfChaosRateBenefitRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.CrywolfChaosRateBenefitRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("B4E7D2C9-8A15-4F6B-9D3E-7C2A1F5B9E61")]
[BelongsToGroup(CrywolfGroupHandlerPlugIn.GroupKey)]
internal class CrywolfChaosRateBenefitRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => CrywolfChaosRateBenefitRequest.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < CrywolfChaosRateBenefitRequest.Length)
        {
            return;
        }

        var rate = CrywolfPlugIn.GetContext(player.GameContext)?.ChaosRateBenefit ?? 0;
        await player.InvokeViewPlugInAsync<ICrywolfEventViewPlugIn>(p => p.ShowChaosRateBenefitAsync(rate)).ConfigureAwait(false);
    }
}
