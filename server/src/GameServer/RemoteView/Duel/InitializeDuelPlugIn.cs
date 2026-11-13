// <copyright file="InitializeDuelPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Duel;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.Duel;
using HNMO.GameServer.RemoteView.World;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IInitializeDuelPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.InitializeDuelPlugIn_Name), Description = nameof(PlugInResources.InitializeDuelPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("41ECFA38-3EAE-4408-B7AC-82F26D8DCCD7")]
[MinimumClient(4, 0, ClientLanguage.Invariant)]
public class InitializeDuelPlugIn : IInitializeDuelPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="InitializeDuelPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public InitializeDuelPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask InitializeDuelAsync(DuelRoom duelRoom)
    {
        var player1 = this._player == duelRoom.Opponent ? duelRoom.Opponent : duelRoom.Requester;
        var player2 = this._player == duelRoom.Opponent ? duelRoom.Requester : duelRoom.Opponent;
        await this._player.Connection.SendDuelInitAsync(
                0,
                (byte)duelRoom.Index,
                player1.Name,
                player2.Name,
                player1.GetId(this._player),
                player2.GetId(this._player))
            .ConfigureAwait(false);
        if (!duelRoom.IsDuelist(this._player))
        {
            await this._player.Connection.SendDuelHealthBarInitAsync().ConfigureAwait(false);
            await this._player.Connection.SendMagicEffectStatusAsync(true, this._player.GetId(this._player), (byte)EffectNumbers.DuelSpectatorHealthBar).ConfigureAwait(false);
        }
    }
}