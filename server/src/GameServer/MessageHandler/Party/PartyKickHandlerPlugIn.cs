// <copyright file="PartyKickHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Party;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Party;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Handler for party kick packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.PartyKickHandlerPlugIn_Name), Description = nameof(PlugInResources.PartyKickHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("26d0fef9-8171-4098-87ae-030054163509")]
internal class PartyKickHandlerPlugIn : IPacketHandlerPlugIn
{
    private readonly PartyKickAction _action = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => (byte)PacketType.PartyKick;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        PartyPlayerKickRequest message = packet;
        await this._action.KickPlayerAsync(player, message.PlayerIndex).ConfigureAwait(false);
    }
}