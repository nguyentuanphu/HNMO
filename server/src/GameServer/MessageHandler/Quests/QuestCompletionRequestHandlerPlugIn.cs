// <copyright file="QuestCompletionRequestHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Quests;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Quests;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Packet handler for quest completion request packets (0xF6, 0x0D identifier).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.QuestCompletionRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.QuestCompletionRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("CB8A33FA-0060-43E2-92F7-BE4BC23FE0B8")]
[BelongsToGroup(QuestGroupHandlerPlugIn.GroupKey)]
public class QuestCompletionRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly QuestCompletionAction _questCompletionAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => QuestCompletionRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        QuestCompletionRequest request = packet;
        await this._questCompletionAction.CompleteQuestAsync(player, (short)request.QuestGroup, (short)request.QuestNumber).ConfigureAwait(false);
    }
}