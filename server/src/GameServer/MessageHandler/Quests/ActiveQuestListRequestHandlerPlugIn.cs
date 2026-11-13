// <copyright file="ActiveQuestListRequestHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Quests;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.Views.Quest;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Packet handler for the list of active quests request packets (0xF6, 0x1A identifier).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ActiveQuestListRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.ActiveQuestListRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("521ED931-BDB0-422E-8E8A-3CCB1BEB639C")]
[BelongsToGroup(QuestGroupHandlerPlugIn.GroupKey)]
internal class ActiveQuestListRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => ActiveQuestListRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        await player.InvokeViewPlugInAsync<ICurrentlyActiveQuestsPlugIn>(p => p.ShowActiveQuestsAsync()).ConfigureAwait(false);
    }
}