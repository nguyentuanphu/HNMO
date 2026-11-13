// <copyright file="QuestProceedRequestHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Quests;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Quests;
using HNMO.GameLogic.Views.Quest;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Packet handler for quest start proceed packets (0xF6, 0x0B identifier).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.QuestProceedRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.QuestProceedRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("D3773016-F156-4481-B080-A8C087444B78")]
[BelongsToGroup(QuestGroupHandlerPlugIn.GroupKey)]
public class QuestProceedRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly QuestStartAction _questStartAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => QuestProceedRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        QuestProceedRequest request = packet;
        var questGroup = (short)request.QuestGroup;
        var questNumber = (short)request.QuestNumber;
        var questState = player.GetQuestState(questGroup, questNumber);

        if (request.ProceedAction == QuestProceedRequest.QuestProceedAction.AcceptQuest)
        {
            if (questState?.ActiveQuest != null)
            {
                // keep it running and confirm that it started
                await player.InvokeViewPlugInAsync<IQuestStartedPlugIn>(p => p.QuestStartedAsync(questState.ActiveQuest)).ConfigureAwait(false);
            }
            else
            {
                await this._questStartAction.StartQuestAsync(player, (short)request.QuestGroup, (short)request.QuestNumber).ConfigureAwait(false);
            }
        }
        else
        {
            // Refused
            if (player.OpenedNpc?.Definition.Quests
                    .FirstOrDefault(q => q.Group == questGroup && q.StartingNumber == questNumber)
                is { } quest)
            {
                await player.InvokeViewPlugInAsync<IQuestStepInfoPlugIn>(p => p.ShowQuestStepInfoAsync(quest.Group, quest.RefuseNumber)).ConfigureAwait(false);
            }
        }
    }
}