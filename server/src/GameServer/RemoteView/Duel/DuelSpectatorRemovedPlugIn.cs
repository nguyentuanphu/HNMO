// <copyright file="DuelSpectatorRemovedPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Duel;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.Views.Duel;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IDuelSpectatorRemovedPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.DuelSpectatorRemovedPlugIn_Name), Description = nameof(PlugInResources.DuelSpectatorRemovedPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("89DFA45D-FED2-4FAA-BDB0-683EB337629B")]
[MinimumClient(4, 0, ClientLanguage.Invariant)]
public class DuelSpectatorRemovedPlugIn : IDuelSpectatorRemovedPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="DuelSpectatorRemovedPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public DuelSpectatorRemovedPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask SpectatorRemovedAsync(Player spectator)
    {
        await this._player.Connection.SendDuelSpectatorRemovedAsync(spectator.Name).ConfigureAwait(false);
    }
}