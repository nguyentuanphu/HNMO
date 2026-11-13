// <copyright file="EventQuestStateRequestHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Quests;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.Views.Quest;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Packet handler for event quest state request packets (0xF6, 0x21 identifier).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.EventQuestStateRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.EventQuestStateRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("907FFFF8-0CF2-4DCD-931F-8AA17FE2EF3D")]
[BelongsToGroup(QuestGroupHandlerPlugIn.GroupKey)]
public class EventQuestStateRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => EventQuestStateListRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        await player.InvokeViewPlugInAsync<IQuestEventResponsePlugIn>(p => p.ShowActiveEventQuestsAsync()).ConfigureAwait(false);
    }
}