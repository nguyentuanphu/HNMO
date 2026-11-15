// <copyright file="HnmoAccountSafetyViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IHnmoAccountSafetyViewPlugIn"/> which sends HNMO's account safety
/// state to the game client. Unasked updates only go to a client which requested the state once (the web game).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoAccountSafetyViewPlugIn_Name), Description = nameof(PlugInResources.HnmoAccountSafetyViewPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("F17B4D38-6C2A-49E5-8B93-0D5A2E71C6B4")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class HnmoAccountSafetyViewPlugIn : IHnmoAccountSafetyViewPlugIn
{
    private readonly RemotePlayer _player;
    private bool _isSupportedByClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HnmoAccountSafetyViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public HnmoAccountSafetyViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowAccountSafetyAsync(string json, bool requested)
    {
        if (requested)
        {
            this._isSupportedByClient = true;
        }

        if (!this._isSupportedByClient)
        {
            return;
        }

        await this._player.Connection.SendHnmoAccountSafetyStateAsync(json).ConfigureAwait(false);
    }
}
