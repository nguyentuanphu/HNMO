// <copyright file="HnmoGiftCodesViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IHnmoGiftCodesViewPlugIn"/> which sends HNMO's gift codes state to the
/// game client. Unasked updates only go to a client which requested the gift codes state once (the web game).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoGiftCodesViewPlugIn_Name), Description = nameof(PlugInResources.HnmoGiftCodesViewPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("5E81C3A9-04D7-4B2F-A6E3-7C19D84F0B26")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class HnmoGiftCodesViewPlugIn : IHnmoGiftCodesViewPlugIn
{
    private readonly RemotePlayer _player;
    private bool _isSupportedByClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HnmoGiftCodesViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public HnmoGiftCodesViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowGiftCodesAsync(string json, bool requested)
    {
        if (requested)
        {
            this._isSupportedByClient = true;
        }

        if (!this._isSupportedByClient)
        {
            return;
        }

        await this._player.Connection.SendHnmoGiftCodesStateAsync(json).ConfigureAwait(false);
    }
}
