// <copyright file="PartyResponseHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Party;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Party;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Handler for party response packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.PartyResponseHandlerPlugIn_Name), Description = nameof(PlugInResources.PartyResponseHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("bd1e7c33-a80e-439f-b8e2-b2c22a68126b")]
internal class PartyResponseHandlerPlugIn : IPacketHandlerPlugIn
{
    private readonly PartyResponseAction _action = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => PartyInviteResponse.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        PartyInviteResponse message = packet;
        await this._action.HandleResponseAsync(player, message.Accepted).ConfigureAwait(false);
    }
}