// <copyright file="HnmoResetsViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IHnmoResetsViewPlugIn"/> which sends HNMO's reset state to the game
/// client. Unasked updates only go to a client which requested the reset state once (the web game).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoResetsViewPlugIn_Name), Description = nameof(PlugInResources.HnmoResetsViewPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("A84C2F17-5E39-4D60-B1A7-0F3E86C94D25")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class HnmoResetsViewPlugIn : IHnmoResetsViewPlugIn
{
    private readonly RemotePlayer _player;
    private bool _isSupportedByClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HnmoResetsViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public HnmoResetsViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowResetsAsync(string json, bool requested)
    {
        if (requested)
        {
            this._isSupportedByClient = true;
        }

        if (!this._isSupportedByClient)
        {
            return;
        }

        await this._player.Connection.SendHnmoResetsStateAsync(json).ConfigureAwait(false);
    }
}
