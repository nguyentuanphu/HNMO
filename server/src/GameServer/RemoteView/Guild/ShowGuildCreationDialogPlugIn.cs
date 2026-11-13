// <copyright file="ShowGuildCreationDialogPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Guild;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views.Guild;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IShowGuildCreationDialogPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowGuildCreationDialogPlugIn_Name), Description = nameof(PlugInResources.ShowGuildCreationDialogPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("ed6fbe5f-7a27-477d-b238-e6e77cf113d8")]
public class ShowGuildCreationDialogPlugIn : IShowGuildCreationDialogPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowGuildCreationDialogPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowGuildCreationDialogPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowGuildCreationDialogAsync()
    {
        await this._player.Connection.SendShowGuildCreationDialogAsync().ConfigureAwait(false);
    }
}