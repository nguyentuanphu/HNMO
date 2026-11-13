// <copyright file="DuelStartResponseHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Duel;

using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Duel;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Handler for duel start response packets (new duel system).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.DuelStartResponseHandlerPlugIn_Name), Description = nameof(PlugInResources.DuelStartResponseHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("C2A7FD08-3F93-467E-AE8B-13F97B7F7888")]
[MinimumClient(4, 0, ClientLanguage.Invariant)]
[BelongsToGroup(DuelGroupHandlerPlugIn.GroupKey)]
internal class DuelStartResponseHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly DuelActions _action = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => DuelStartResponse.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < DuelStartResponse.Length)
        {
            return;
        }

        DuelStartResponse response = packet;
        var accepted = response.Response;
        var targetId = response.PlayerId;
        var targetName = response.PlayerName;

        var target = await player.GetObservingPlayerWithIdAsync(targetId).ConfigureAwait(false);

        if (target is null)
        {
            player.Logger.LogWarning($"Player {player.Name} sent response for duel player with id {targetId}, but the player was not found.");
            return;
        }

        if (target.Name != targetName)
        {
            player.Logger.LogWarning($"Player {player.Name} sent duel response for player {target.Name} with id {targetId}, but the names didn't match.");
            return;
        }

        await this._action.HandleDuelResponseAsync(player, target, accepted).ConfigureAwait(false);
    }
}