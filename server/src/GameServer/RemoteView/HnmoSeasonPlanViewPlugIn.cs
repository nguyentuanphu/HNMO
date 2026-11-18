// <copyright file="HnmoSeasonPlanViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Default remote view implementation for <see cref="IHnmoSeasonPlanViewPlugIn"/>.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Season Plan View", Description = "Sends Season Plan stage timeline state to web game clients.")]
[Guid("7A1E6D32-9C05-41F8-8E4B-5D2A903F14C6")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class HnmoSeasonPlanViewPlugIn : IHnmoSeasonPlanViewPlugIn
{
    private readonly RemotePlayer _player;
    private bool _isSupportedByClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HnmoSeasonPlanViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The remote player.</param>
    public HnmoSeasonPlanViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowSeasonPlanAsync(string json, bool requested)
    {
        if (requested)
        {
            this._isSupportedByClient = true;
        }

        if (!this._isSupportedByClient)
        {
            return;
        }

        await this._player.Connection.SendHnmoSeasonPlanStateAsync(json).ConfigureAwait(false);
    }
}
