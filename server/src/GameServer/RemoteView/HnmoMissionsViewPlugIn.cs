// <copyright file="HnmoMissionsViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IHnmoMissionsViewPlugIn"/> which sends HNMO's missions state to the
/// game client. Unasked updates only go to a client which requested the missions once (the web game).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoMissionsViewPlugIn_Name), Description = nameof(PlugInResources.HnmoMissionsViewPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("A3D59C17-6E42-4B8F-9D01-5C2E7B8A4F63")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class HnmoMissionsViewPlugIn : IHnmoMissionsViewPlugIn
{
    private readonly RemotePlayer _player;
    private bool _isSupportedByClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HnmoMissionsViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public HnmoMissionsViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowMissionsAsync(string json, bool requested)
    {
        if (requested)
        {
            this._isSupportedByClient = true;
        }

        if (!this._isSupportedByClient)
        {
            return;
        }

        await this._player.Connection.SendHnmoMissionsStateAsync(json).ConfigureAwait(false);
    }
}
