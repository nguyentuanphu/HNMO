// <copyright file="GuildCreateHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Guild;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Guild;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Handler for guild create packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.GuildCreateHandlerPlugIn_Name), Description = nameof(PlugInResources.GuildCreateHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("0aae71c1-72df-47d6-af88-cddc5d5c7311")]
[MinimumClient(1, 0, ClientLanguage.Invariant)]
internal class GuildCreateHandlerPlugIn : IPacketHandlerPlugIn
{
    private readonly GuildCreateAction _createAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => GuildCreateRequest.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        GuildCreateRequest request = packet;
        await this._createAction.CreateGuildAsync(player, request.GuildName, request.GuildEmblem.ToArray()).ConfigureAwait(false);
    }
}