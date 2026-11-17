// <copyright file="HnmoMarriageViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Sends <see cref="HnmoMarriageState"/> packets (<c>F5 37</c>) to clients supporting HNMO Marriage.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Marriage view", Description = "Sends marriage status and couple list packets to the client.")]
[Guid("3C7E9B2A-15F4-48D6-A09E-5E21B7C4D8F6")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public sealed class HnmoMarriageViewPlugIn : IHnmoMarriageViewPlugIn
{
    private readonly RemotePlayer _player;
    private bool _isSupportedByClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HnmoMarriageViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The remote player.</param>
    public HnmoMarriageViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowMarriageAsync(string json, bool requested)
    {
        if (requested)
        {
            this._isSupportedByClient = true;
        }

        if (!this._isSupportedByClient)
        {
            return;
        }

        await this._player.Connection.SendHnmoMarriageStateAsync(json).ConfigureAwait(false);
    }
}
