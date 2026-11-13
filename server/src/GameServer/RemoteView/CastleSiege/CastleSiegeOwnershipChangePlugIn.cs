// <copyright file="CastleSiegeOwnershipChangePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.CastleSiege;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views.CastleSiege;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="ICastleSiegeOwnershipChangePlugIn"/>
/// which announces the new castle owner to the game client.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.CastleSiegeOwnershipChangePlugIn_Name), Description = nameof(PlugInResources.CastleSiegeOwnershipChangePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("11A4F0AF-7EA7-4534-8470-50EEF31BF464")]
public class CastleSiegeOwnershipChangePlugIn : ICastleSiegeOwnershipChangePlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="CastleSiegeOwnershipChangePlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public CastleSiegeOwnershipChangePlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public ValueTask ShowOwnershipChangeAsync(string guildName)
        => this._player.Connection?.SendCastleSiegeBattleProcessAsync(
            CastleSiegeBattleProcessState.CrownRegistrationSucceeded,
            guildName) ?? default;
}
