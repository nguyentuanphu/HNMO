// <copyright file="GuildRequestHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Guild;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Guild;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Handler for guild requests.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.GuildRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.GuildRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("733b8b1d-7e39-4c5a-b134-d1aac2e33216")]
internal class GuildRequestHandlerPlugIn : IPacketHandlerPlugIn
{
    private readonly GuildRequestAction _requestAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => GuildJoinRequest.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        GuildJoinRequest request = packet;
        await this._requestAction.RequestGuildAsync(player, request.GuildMasterPlayerId).ConfigureAwait(false);
    }
}