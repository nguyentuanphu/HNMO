// <copyright file="CastleSiegeUnregisterHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.CastleSiege;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.CastleSiege.Actions;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Handles Castle Siege guild unregistration requests.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.CastleSiegeUnregisterHandlerPlugIn_Name), Description = nameof(PlugInResources.CastleSiegeUnregisterHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("F8DFF1B0-1DDA-4988-AAC4-65004A7FF087")]
[BelongsToGroup(CastleSiegeGroupHandlerPlugIn.GroupKey)]
internal class CastleSiegeUnregisterHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly CastleSiegeUnregisterGuildAction _action = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => CastleSiegeUnregisterRequest.SubCode;

    /// <inheritdoc/>
    public ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < CastleSiegeUnregisterRequest.Length)
        {
            return ValueTask.CompletedTask;
        }

        var request = new CastleSiegeUnregisterRequest(packet);
        return this._action.UnregisterAsync(
            player,
            CastleSiegeHandlerContext.Get(player),
            request.IsGivingUp);
    }
}
