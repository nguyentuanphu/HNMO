// <copyright file="GuildListRequestHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Guild;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Guild;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Handler for guild list request packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.GuildListRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.GuildListRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("cf021a55-8ac7-45e4-a1c2-b61eadea9099")]
internal class GuildListRequestHandlerPlugIn : IPacketHandlerPlugIn
{
    private readonly GuildListRequestAction _requestAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => GuildListRequest.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        await this._requestAction.RequestGuildListAsync(player).ConfigureAwait(false);
    }
}