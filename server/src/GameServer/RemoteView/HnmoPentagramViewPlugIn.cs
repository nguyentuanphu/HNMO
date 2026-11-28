// <copyright file="HnmoPentagramViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Default remote view implementation for <see cref="IHnmoPentagramViewPlugIn"/>.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Pentagram View", Description = "Sends the Season 9 pentagram state and elemental hits to web game clients.")]
[Guid("5B7C3E19-2A64-4F0D-9E81-6D4A2B9C7F31")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class HnmoPentagramViewPlugIn : IHnmoPentagramViewPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="HnmoPentagramViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The remote player.</param>
    public HnmoPentagramViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public ValueTask ShowPentagramAsync(string json)
        => this._player.Connection.SendHnmoPentagramStateAsync(json);

    /// <inheritdoc />
    public ValueTask ShowElementalHitAsync(ushort targetId, int damage, byte element)
        => this._player.Connection.SendHnmoElementalHitAsync(targetId, unchecked((uint)damage), element);
}
