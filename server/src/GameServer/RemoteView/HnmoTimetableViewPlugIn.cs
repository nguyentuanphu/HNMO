// <copyright file="HnmoTimetableViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Sends <see cref="HnmoTimetableState"/> packets (<c>F5 33</c>) to clients supporting the HNMO timetable window.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Timetable view", Description = "Sends event and boss timetable state packets to the client.")]
[Guid("74C8B1E2-0F93-462D-8954-2D51B3E60A7B")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public sealed class HnmoTimetableViewPlugIn : IHnmoTimetableViewPlugIn
{
    private readonly RemotePlayer _player;
    private bool _isSupportedByClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HnmoTimetableViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The remote player.</param>
    public HnmoTimetableViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowTimetableAsync(string json, bool requested)
    {
        if (requested)
        {
            this._isSupportedByClient = true;
        }

        if (!this._isSupportedByClient)
        {
            return;
        }

        await this._player.Connection.SendHnmoTimetableStateAsync(json).ConfigureAwait(false);
    }
}
