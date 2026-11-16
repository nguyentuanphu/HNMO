// <copyright file="HnmoTicketLoginHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions;
using HNMO.GameServer.RemoteView;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;
using Microsoft.Extensions.Logging;

/// <summary>
/// Packet handler for web game login using a one-time ticket (<see cref="HnmoTicketLogin"/>).
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.GameLogic.Properties.PlugInResources.HnmoTicketLoginHandlerPlugIn_Name), Description = nameof(HNMO.GameLogic.Properties.PlugInResources.HnmoTicketLoginHandlerPlugIn_Description), ResourceType = typeof(HNMO.GameLogic.Properties.PlugInResources))]
[Guid("E7A2C940-8D51-4F3B-B824-9C4E12A7F563")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class HnmoTicketLoginHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly LoginAction _loginAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => HnmoTicketLogin.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < HnmoTicketLogin.Length)
        {
            return;
        }

        HnmoTicketLogin message = packet;
        var username = message.Username;
        var ticket = message.Ticket;

        if (player.Logger.IsEnabled(LogLevel.Debug))
        {
            player.Logger.LogDebug("User tries to log in with a web ticket. username:{Username}", username);
        }

        await this._loginAction.LoginWithTicketAsync(player, username, ticket).ConfigureAwait(false);
        if (player is RemotePlayer remotePlayer)
        {
            remotePlayer.ClientVersion = new ClientVersion(106, 3, ClientLanguage.Invariant);
        }
    }
}
