// <copyright file="RemoveAllianceGuildHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Guild;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Guild;
using HNMO.PlugIns;
using RemoveAllianceGuildRequest = HNMO.Network.Packets.ClientToServer.RemoveAllianceGuildRequest;

/// <summary>
/// Handler for remove alliance guild request packets (C1 EB 01).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.RemoveAllianceGuildHandlerPlugIn_Name), Description = nameof(PlugInResources.RemoveAllianceGuildHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("D5E6F7A8-B9C0-4D1E-2F3A-4B5C6D7E8F9A")]
[BelongsToGroup(AllianceGroupHandlerPlugIn.GroupKey)]
internal class RemoveAllianceGuildHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly GuildRelationshipChangeAction _changeAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => RemoveAllianceGuildRequest.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        RemoveAllianceGuildRequest request = packet;
        var targetGuildName = request.GuildName;
        if (string.IsNullOrWhiteSpace(targetGuildName))
        {
            return;
        }

        await this._changeAction.RequestLeaveAllianceAsync(player, targetGuildName).ConfigureAwait(false);
    }
}
