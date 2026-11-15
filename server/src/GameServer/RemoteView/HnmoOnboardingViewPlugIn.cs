// <copyright file="HnmoOnboardingViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IHnmoOnboardingViewPlugIn"/> which sends HNMO's new-player guide to the
/// game client. Unasked updates only go to a client which requested the guide once (the web game).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoOnboardingViewPlugIn_Name), Description = nameof(PlugInResources.HnmoOnboardingViewPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("7B4E19C3-2D85-4A6F-9E01-C3F58A2D6B47")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class HnmoOnboardingViewPlugIn : IHnmoOnboardingViewPlugIn
{
    private readonly RemotePlayer _player;
    private bool _isSupportedByClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HnmoOnboardingViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public HnmoOnboardingViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowOnboardingAsync(string json, bool requested)
    {
        if (requested)
        {
            this._isSupportedByClient = true;
        }

        if (!this._isSupportedByClient)
        {
            return;
        }

        await this._player.Connection.SendHnmoOnboardingStateAsync(json).ConfigureAwait(false);
    }
}
