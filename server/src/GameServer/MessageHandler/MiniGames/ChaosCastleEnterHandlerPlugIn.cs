// <copyright file="ChaosCastleEnterHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.MiniGames;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlayerActions.MiniGames;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Handler for chaos castle enter request packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ChaosCastleEnterHandlerPlugIn_Name), Description = nameof(PlugInResources.ChaosCastleEnterHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("D4F0076F-86D2-4712-B9FD-6B1C58B71969")]
internal class ChaosCastleEnterHandlerPlugIn : IPacketHandlerPlugIn
{
    /// <summary>
    /// The game action which contains the logic to enter the mini game.
    /// </summary>
    private readonly EnterMiniGameAction _enterAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => ChaosCastleEnterRequest.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < ChaosCastleEnterRequest.Length
            || player.SelectedCharacter?.CharacterClass is null)
        {
            return;
        }

        ChaosCastleEnterRequest request = packet;
        if (request.Header.SubCode != ChaosCastleEnterRequest.SubCode)
        {
            return;
        }

        var actualLevel = request.CastleLevel;
        var ticketIndex = request.TicketItemInventoryIndex;
        var definition = player.GetSuitableMiniGameDefinition(MiniGameType.ChaosCastle, actualLevel);

        await this._enterAction.TryEnterMiniGameAsync(player, MiniGameType.ChaosCastle, definition?.GameLevel ?? actualLevel, ticketIndex).ConfigureAwait(false);
    }
}