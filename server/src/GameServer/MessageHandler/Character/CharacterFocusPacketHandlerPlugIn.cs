// <copyright file="CharacterFocusPacketHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Character;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Character;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Packet handler for character focus packets (0xF3, 0x15 identifier).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.CharacterFocusPacketHandlerPlugIn_Name), Description = nameof(PlugInResources.CharacterFocusPacketHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("8687C77F-E26C-4510-AD85-E5F51305DE2A")]
[BelongsToGroup(CharacterGroupHandlerPlugIn.GroupKey)]
internal class CharacterFocusPacketHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly FocusCharacterAction _focusCharacterAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => 0x15;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        FocusCharacter message = packet;
        await this._focusCharacterAction.FocusCharacterAsync(player, message.Name).ConfigureAwait(false);
    }
}