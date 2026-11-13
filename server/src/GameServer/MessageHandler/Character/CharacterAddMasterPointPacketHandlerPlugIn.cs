// <copyright file="CharacterAddMasterPointPacketHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Character;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Character;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Sub packet handler for master skill point add requests.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.CharacterAddMasterPointPacketHandlerPlugIn_Name), Description = nameof(PlugInResources.CharacterAddMasterPointPacketHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("9F39C1FB-26F7-460F-A21B-C4DFEA234E66")]
[BelongsToGroup(CharacterGroupHandlerPlugIn.GroupKey)]
internal class CharacterAddMasterPointPacketHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly AddMasterPointAction _addMasterPointAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => 0x52;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        AddMasterSkillPoint message = packet;
        await this._addMasterPointAction.AddMasterPointAsync(player, message.SkillId).ConfigureAwait(false);
    }
}