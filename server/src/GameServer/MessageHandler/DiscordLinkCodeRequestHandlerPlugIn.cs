// <copyright file="DiscordLinkCodeRequestHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Discord;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Handler for the request of a code to link the account to a Discord user.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.DiscordLinkCodeRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.DiscordLinkCodeRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("5D383E4B-82FC-448A-8AAB-B38682150657")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class DiscordLinkCodeRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly DiscordIntegrationAction _action = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => DiscordLinkCodeRequest.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null)
        {
            return;
        }

        await this._action.RequestLinkCodeAsync(player).ConfigureAwait(false);
    }
}
