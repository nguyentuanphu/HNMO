// <copyright file="BloodCastleEnterHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.MiniGames;

using System;
using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.MiniGames;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Handler for blood castle enter request packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.BloodCastleEnterHandlerPlugIn_Name), Description = nameof(PlugInResources.BloodCastleEnterHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("999D6CC6-7B5C-4D0A-89E5-DFC1A1E482FA")]
internal class BloodCastleEnterHandlerPlugIn : IPacketHandlerPlugIn
{
    /// <summary>
    /// The game action which contains the logic to enter the mini game.
    /// </summary>
    private readonly EnterMiniGameAction _enterAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => BloodCastleEnterRequest.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < BloodCastleEnterRequest.Length
            || player.SelectedCharacter?.CharacterClass is null)
        {
            return;
        }

        BloodCastleEnterRequest request = packet;
        var actualLevel = request.CastleLevel;
        var ticketIndex = request.TicketItemInventoryIndex;
        await this._enterAction.TryEnterMiniGameAsync(player, MiniGameType.BloodCastle, actualLevel, (byte)ticketIndex).ConfigureAwait(false);
    }
}