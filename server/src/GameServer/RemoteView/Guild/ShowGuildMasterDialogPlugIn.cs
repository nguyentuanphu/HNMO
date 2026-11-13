// <copyright file="ShowGuildMasterDialogPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Guild;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views.Guild;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IShowGuildMasterDialogPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowGuildMasterDialogPlugIn_Name), Description = nameof(PlugInResources.ShowGuildMasterDialogPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("77d430e0-8bed-425b-8bb5-7bbafa9bfbff")]
public class ShowGuildMasterDialogPlugIn : IShowGuildMasterDialogPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowGuildMasterDialogPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowGuildMasterDialogPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowGuildMasterDialogAsync()
    {
        await this._player.Connection.SendShowGuildMasterDialogAsync().ConfigureAwait(false);
    }
}