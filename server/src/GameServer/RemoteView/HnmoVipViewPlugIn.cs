// <copyright file="HnmoVipViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>Returns safe VIP JSON only in reply to the web session's own correlated request.</summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoVipViewPlugIn_Name), Description = nameof(PlugInResources.HnmoVipViewPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("DD76EF82-1252-4EB2-B0F6-2E9D1DA4EF59")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public sealed class HnmoVipViewPlugIn : IHnmoVipViewPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>Initializes a new instance of the <see cref="HnmoVipViewPlugIn"/> class.</summary>
    /// <param name="player">The authenticated remote player.</param>
    public HnmoVipViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public ValueTask ShowVipAsync(uint requestId, string json)
        => this._player.Connection.SendHnmoVipStateAsync(requestId, json);
}
