// <copyright file="HnmoTopRacesViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IHnmoTopRacesViewPlugIn"/> which sends HNMO's top races state to the
/// game client. Unasked updates only go to a client which requested the top races state once (the web game).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoTopRacesViewPlugIn_Name), Description = nameof(PlugInResources.HnmoTopRacesViewPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("C4E92B17-5A3D-4F06-8B71-D29A6E0F3C45")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class HnmoTopRacesViewPlugIn : IHnmoTopRacesViewPlugIn
{
    private readonly RemotePlayer _player;
    private bool _isSupportedByClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HnmoTopRacesViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public HnmoTopRacesViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowTopRacesAsync(string json, bool requested)
    {
        if (requested)
        {
            this._isSupportedByClient = true;
        }

        if (!this._isSupportedByClient)
        {
            return;
        }

        await this._player.Connection.SendHnmoTopRacesStateAsync(json).ConfigureAwait(false);
    }
}
