// <copyright file="CharacterStatIncreasePacketHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Character;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Character;
using HNMO.GameServer.RemoteView;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Packet handler for character stat increase packets (0xF3, 0x06 identifier).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.CharacterStatIncreasePacketHandlerPlugIn_Name), Description = nameof(PlugInResources.CharacterStatIncreasePacketHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("5DC06689-B2DD-4CA2-8F93-97FB1198BA70")]
[BelongsToGroup(CharacterGroupHandlerPlugIn.GroupKey)]
internal class CharacterStatIncreasePacketHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly IncreaseStatsAction _increaseStatsAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => 6;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        IncreaseCharacterStatPoint message = packet;
        await this._increaseStatsAction.IncreaseStatsAsync(player, message.StatType.GetAttributeDefinition()).ConfigureAwait(false);
    }
}