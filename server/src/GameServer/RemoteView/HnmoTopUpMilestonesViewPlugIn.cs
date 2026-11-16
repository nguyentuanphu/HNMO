// <copyright file="HnmoTopUpMilestonesViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Sends <see cref="HnmoTopUpMilestonesState"/> packets to clients that support HNMO top-up milestones.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Top-up milestones view", Description = "Sends HNMO top-up milestones state packets to the client.")]
[Guid("2A19E6D4-73B8-4E2F-9C41-68B2D3F71204")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class HnmoTopUpMilestonesViewPlugIn : IHnmoTopUpMilestonesViewPlugIn
{
    private readonly RemotePlayer _player;
    private bool _isSupportedByClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HnmoTopUpMilestonesViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The remote player.</param>
    public HnmoTopUpMilestonesViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowTopUpMilestonesAsync(string json, bool requested)
    {
        if (requested)
        {
            this._isSupportedByClient = true;
        }

        if (!this._isSupportedByClient)
        {
            return;
        }

        await this._player.Connection.SendHnmoTopUpMilestonesStateAsync(json).ConfigureAwait(false);
    }
}
