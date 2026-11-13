// <copyright file="QuestCancelRequestHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Quests;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Quests;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Packet handler for quest cancel request packets (0xF6, 0x0F identifier).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.QuestCancelRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.QuestCancelRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("996E33ED-14B7-4165-9E6C-AE583974D0B7")]
[BelongsToGroup(QuestGroupHandlerPlugIn.GroupKey)]
public class QuestCancelRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly QuestCancelAction _questCancelAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => QuestCancelRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        QuestCancelRequest request = packet;
        await this._questCancelAction.CancelQuestAsync(player, (short)request.QuestGroup, (short)request.QuestNumber).ConfigureAwait(false);
    }
}