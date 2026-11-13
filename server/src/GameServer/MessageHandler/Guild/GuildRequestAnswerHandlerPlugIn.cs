// <copyright file="GuildRequestAnswerHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Guild;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Guild;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Handler for guild request answer packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.GuildRequestAnswerHandlerPlugIn_Name), Description = nameof(PlugInResources.GuildRequestAnswerHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("30828257-ec3a-4b99-981d-ec105fc5e82e")]
internal class GuildRequestAnswerHandlerPlugIn : IPacketHandlerPlugIn
{
    private readonly GuildRequestAnswerAction _answerAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => GuildJoinResponse.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        GuildJoinResponse response = packet;
        await this._answerAction.AnswerRequestAsync(player, response.Accepted).ConfigureAwait(false);
    }
}