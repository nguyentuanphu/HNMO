// <copyright file="PartyMemberRemovedPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Party;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views.Party;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IPartyMemberRemovedPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.PartyMemberRemovedPlugIn_Name), Description = nameof(PlugInResources.PartyMemberRemovedPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("d69b37b3-9e7f-40d1-9260-ba7a4f6369a2")]
public class PartyMemberRemovedPlugIn : IPartyMemberRemovedPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="PartyMemberRemovedPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public PartyMemberRemovedPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask PartyMemberRemovedAsync(byte index)
    {
        await this._player.Connection.SendRemovePartyMemberAsync(index).ConfigureAwait(false);
    }
}