// <copyright file="HnmoSeasonNineClientViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.GameServer.Properties;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Sends <see cref="HnmoSeasonNineClientState"/> (<c>F5 59</c>); only ever as the answer to the web client's request,
/// so a client which doesn't know the packet never gets it.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoSeasonNineClientViewPlugIn_Name), Description = nameof(PlugInResources.HnmoSeasonNineClientViewPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("0B6D2F84-3E7A-4C19-9F5D-8A21C4E6B703")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class HnmoSeasonNineClientViewPlugIn : IHnmoSeasonNineClientViewPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="HnmoSeasonNineClientViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The remote player.</param>
    public HnmoSeasonNineClientViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowSeasonNineClientStateAsync(string json)
    {
        await this._player.Connection.SendHnmoSeasonNineClientStateAsync(json).ConfigureAwait(false);
    }
}
