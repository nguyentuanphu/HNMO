// <copyright file="CloseVaultPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Vault;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views.Vault;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="ICloseVaultPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.CloseVaultPlugIn_Name), Description = nameof(PlugInResources.CloseVaultPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("3030e5aa-01a2-4523-b42e-2eef16f4b58f")]
public class CloseVaultPlugIn : ICloseVaultPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="CloseVaultPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public CloseVaultPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask CloseVaultAsync()
    {
        await this._player.Connection.SendVaultClosedAsync().ConfigureAwait(false);
    }
}