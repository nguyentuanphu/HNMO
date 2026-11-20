// <copyright file="HnmoBugReportViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>Sends the bug report window's state (F5 55) to web clients.</summary>
[PlugIn]
[Display(Name = "HNMO bug report view", Description = "Sends the bug report window's state to web clients.")]
[Guid("A37E5C92-1B4D-4F68-8C03-D9E2B6F14A75")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public sealed class HnmoBugReportViewPlugIn : IHnmoBugReportViewPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>Initializes a new instance of the <see cref="HnmoBugReportViewPlugIn"/> class.</summary>
    /// <param name="player">The remote player.</param>
    public HnmoBugReportViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowBugReportsAsync(string json)
    {
        await this._player.Connection.SendHnmoBugReportStateAsync(json).ConfigureAwait(false);
    }
}
