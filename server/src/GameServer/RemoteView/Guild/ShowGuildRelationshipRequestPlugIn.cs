// <copyright file="ShowGuildRelationshipRequestPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Guild;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.Guild;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IShowGuildRelationshipRequestPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowGuildRelationshipRequestPlugIn_Name), Description = nameof(PlugInResources.ShowGuildRelationshipRequestPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("F7A8B9C0-D1E2-4F3A-4B5C-6D7E8F9A0B1C")]
public class ShowGuildRelationshipRequestPlugIn : IShowGuildRelationshipRequestPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowGuildRelationshipRequestPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowGuildRelationshipRequestPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowRequestAsync(Player requestingGuildMaster, GameLogic.Views.Guild.GuildRelationshipType relationshipType, GameLogic.Views.Guild.GuildRelationshipRequestType requestType)
    {
        await this._player.Connection.SendGuildRelationshipRequestAsync(
            relationshipType.Convert(),
            requestType.Convert(),
            requestingGuildMaster.GetId(this._player)).ConfigureAwait(false);
    }
}
