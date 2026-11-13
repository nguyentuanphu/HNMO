// <copyright file="UpdateCharacterBaseStatsExtendedPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Character;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.Views.Character;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// The extended implementation of the <see cref="IUpdateCharacterBaseStatsPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.UpdateCharacterBaseStatsExtendedPlugIn_Name), Description = nameof(PlugInResources.UpdateCharacterBaseStatsExtendedPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("851C4579-FB3D-454C-A238-217542E8E6B9")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class UpdateCharacterBaseStatsExtendedPlugIn : IUpdateCharacterBaseStatsPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateCharacterBaseStatsExtendedPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public UpdateCharacterBaseStatsExtendedPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask UpdateCharacterBaseStatsAsync()
    {
        var connection = this._player.Connection;
        if (connection is null || this._player.Account is null)
        {
            return;
        }

        await connection.SendBaseStatsExtendedAsync(
                (uint)this._player.Attributes![Stats.BaseStrength],
                (uint)this._player.Attributes[Stats.BaseAgility],
                (uint)this._player.Attributes[Stats.BaseVitality],
                (uint)this._player.Attributes[Stats.BaseEnergy],
                (uint)this._player.Attributes[Stats.BaseLeadership])
            .ConfigureAwait(false);
    }
}