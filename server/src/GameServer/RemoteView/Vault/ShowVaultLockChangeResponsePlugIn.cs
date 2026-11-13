// <copyright file="ShowVaultLockChangeResponsePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Vault;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views.Vault;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IShowVaultLockChangeResponse"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowVaultLockChangeResponsePlugIn_Name), Description = nameof(PlugInResources.ShowVaultLockChangeResponsePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("3F8A1129-A139-4BD7-9122-EE0D189C5F39")]
public class ShowVaultLockChangeResponsePlugIn : IShowVaultLockChangeResponse
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowVaultLockChangeResponsePlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowVaultLockChangeResponsePlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowResponseAsync(VaultLockChangeResult result)
    {
        await this._player.Connection.SendVaultProtectionInformationAsync(this.GetVaultState(result)).ConfigureAwait(false);
    }

    private VaultProtectionInformation.VaultProtectionState GetVaultState(VaultLockChangeResult result)
    {
        return result switch
        {
            VaultLockChangeResult.RemovePinFailedByWrongPassword => VaultProtectionInformation.VaultProtectionState.RemovePinFailedByWrongPassword,
            VaultLockChangeResult.SetPinFailedBecauseLock => VaultProtectionInformation.VaultProtectionState.SetPinFailedBecauseLock,
            VaultLockChangeResult.UnlockFailedByWrongPin => VaultProtectionInformation.VaultProtectionState.UnlockFailedByWrongPin,
            VaultLockChangeResult.Unlocked => VaultProtectionInformation.VaultProtectionState.Unlocked,
            _ => throw new System.ArgumentOutOfRangeException(nameof(result)),
        };
    }
}