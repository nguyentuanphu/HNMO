// <copyright file="HnmoGiftBoxViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>Sends own-account Gift Box mailbox JSON state in reply to correlated web game requests.</summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoGiftBoxViewPlugIn_Name), Description = nameof(PlugInResources.HnmoGiftBoxViewPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("62988F59-99C4-4034-8B4A-4B9E38CD597C")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public sealed class HnmoGiftBoxViewPlugIn : IHnmoGiftBoxViewPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>Initializes a new instance of the <see cref="HnmoGiftBoxViewPlugIn"/> class.</summary>
    /// <param name="player">The authenticated remote player.</param>
    public HnmoGiftBoxViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public ValueTask ShowGiftBoxAsync(uint requestId, string json)
        => this._player.Connection.SendHnmoGiftBoxStateAsync(requestId, json);
}
