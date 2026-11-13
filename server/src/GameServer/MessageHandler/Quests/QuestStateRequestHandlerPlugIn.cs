// <copyright file="QuestStateRequestHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Quests;

using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Quests;
using HNMO.GameLogic.Views.Quest;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Packet handler for quest state request packets (0xF6, 0x1B identifier).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.QuestStateRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.QuestStateRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("AEA553A5-06A7-43D6-9A59-7C64AAC768C7")]
[BelongsToGroup(QuestGroupHandlerPlugIn.GroupKey)]
internal class QuestStateRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => QuestStateRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        QuestStateRequest request = packet;
        var questState = player.GetQuestState((short)request.QuestGroup, (short)request.QuestNumber);
        if (questState is null)
        {
            player.Logger.LogError($"Quest state not found. Group {request.QuestGroup}, Number {request.QuestNumber}, Player {player}");
        }
        else
        {
            await player.InvokeViewPlugInAsync<IQuestStateResponsePlugIn>(p => p.ShowQuestStateAsync(questState)).ConfigureAwait(false);
        }
    }
}