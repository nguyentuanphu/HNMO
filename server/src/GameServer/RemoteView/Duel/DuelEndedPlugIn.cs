// <copyright file="DuelEndedPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Duel;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.Duel;
using HNMO.GameServer.RemoteView.World;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IDuelEndedPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.DuelEndedPlugIn_Name), Description = nameof(PlugInResources.DuelEndedPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("4FBC822B-F35B-4CB1-AFE6-180243171074")]
[MinimumClient(4, 0, ClientLanguage.Invariant)]
public class DuelEndedPlugIn : IDuelEndedPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="DuelEndedPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public DuelEndedPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask DuelEndedAsync()
    {
        await this._player.Connection.SendDuelEndAsync(0, string.Empty).ConfigureAwait(false);
        await this._player.Connection.SendMagicEffectStatusAsync(false, this._player.GetId(this._player), (byte)EffectNumbers.DuelSpectatorHealthBar).ConfigureAwait(false);
    }
}