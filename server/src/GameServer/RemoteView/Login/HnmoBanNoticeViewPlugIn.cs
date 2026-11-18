// <copyright file="HnmoBanNoticeViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Login;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views.Login;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>Sends structured bilingual ban/mute notices (C2 F5 29) to the connected game client.</summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoBanNoticeViewPlugIn_Name), Description = nameof(PlugInResources.HnmoBanNoticeViewPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("5B7C9D12-8A41-4F3D-9E67-B12F3C4A5D01")]
public class HnmoBanNoticeViewPlugIn : IHnmoBanNoticeViewPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>Initializes a new instance of the <see cref="HnmoBanNoticeViewPlugIn"/> class.</summary>
    /// <param name="player">The remote player.</param>
    public HnmoBanNoticeViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask SendBanNoticeAsync(string json)
    {
        if (this._player.Connection is not { Connected: true } connection)
        {
            return;
        }

        await connection.SendHnmoBanNoticeAsync(json).ConfigureAwait(false);
    }
}
