// <copyright file="BuffRequestHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Quests;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Quests;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Packet handler for NPC buff request packets (0xF6, 0x31 identifier).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.BuffRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.BuffRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("98F31A99-33CE-46FF-98BF-B66EF509C277")]
[BelongsToGroup(QuestGroupHandlerPlugIn.GroupKey)]
public class BuffRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly NpcBuffRequestAction _buffRequestAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => NpcBuffRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        await this._buffRequestAction.RequestBuffAsync(player).ConfigureAwait(false);
    }
}