// <copyright file="AvailableQuestsRequestHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Quests;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.Views.Quest;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Packet handler for available quest request packets (0xF6, 0x30 identifier).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.AvailableQuestsRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.AvailableQuestsRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("12722085-06FE-4D03-848E-89180C017CDB")]
[BelongsToGroup(QuestGroupHandlerPlugIn.GroupKey)]
public class AvailableQuestsRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => AvailableQuestsRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        await player.InvokeViewPlugInAsync<IShowAvailableQuestsPlugIn>(p => p.ShowAvailableQuestsAsync()).ConfigureAwait(false);
    }
}