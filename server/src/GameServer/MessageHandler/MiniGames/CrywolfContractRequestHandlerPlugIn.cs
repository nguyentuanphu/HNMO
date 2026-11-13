// <copyright file="CrywolfContractRequestHandlerPlugIn.cs" company="HNMO">
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
/// Handler for 0xBD/0x03 — CrywolfContractRequest, which an elf sends to contract an altar of the crywolf event.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.CrywolfContractRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.CrywolfContractRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("6F1B9A28-4D7C-4E53-B8A2-3C9E5F1D7B48")]
[BelongsToGroup(CrywolfGroupHandlerPlugIn.GroupKey)]
internal class CrywolfContractRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => CrywolfContractRequest.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < CrywolfContractRequest.Length
            || CrywolfPlugIn.GetContext(player.GameContext) is not { } context)
        {
            return;
        }

        // The field is named after the statue, but the client sends the id of the altar.
        CrywolfContractRequest request = packet;
        await context.ContractAltarAsync(player, request.StatueId).ConfigureAwait(false);
    }
}
