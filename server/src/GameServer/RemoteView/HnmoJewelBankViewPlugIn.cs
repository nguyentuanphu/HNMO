// <copyright file="HnmoJewelBankViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Default remote view implementation for <see cref="IHnmoJewelBankViewPlugIn"/>.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Jewel Bank View", Description = "Sends HNMO Jewel Bank state to web game clients.")]
[Guid("D7E41F82-6B29-4C1E-A895-3C01E6D2F940")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class HnmoJewelBankViewPlugIn : IHnmoJewelBankViewPlugIn
{
    private readonly RemotePlayer _player;
    private bool _isSupportedByClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HnmoJewelBankViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The remote player.</param>
    public HnmoJewelBankViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowJewelBankAsync(string json, bool requested)
    {
        if (requested)
        {
            this._isSupportedByClient = true;
        }

        if (!this._isSupportedByClient)
        {
            return;
        }

        await this._player.Connection.SendHnmoJewelBankStateAsync(json).ConfigureAwait(false);
    }
}
