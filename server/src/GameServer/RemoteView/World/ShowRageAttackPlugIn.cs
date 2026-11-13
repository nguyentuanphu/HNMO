// <copyright file="ShowRageAttackPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.World;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.World;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IShowRageAttackPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowRageAttackPlugIn_Name), Description = nameof(PlugInResources.ShowRageAttackPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("F3E2FA03-AA87-4D61-855C-8ECAF990E108")]
[MinimumClient(6, 0, ClientLanguage.Invariant)]
public class ShowRageAttackPlugIn : IShowRageAttackPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowRageAttackPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowRageAttackPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowAttackAsync(IIdentifiable attacker, IIdentifiable? target, ushort skillId)
    {
        await this._player.Connection.SendRageAttackAsync(skillId, attacker.GetId(this._player), (ushort)(target.GetId(this._player) | 0x8000)).ConfigureAwait(false);
    }
}