// <copyright file="StatIncreaseResultExtendedPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Character;

using System.Runtime.InteropServices;
using HNMO.AttributeSystem;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.Views.Character;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// The extended implementation of the <see cref="IStatIncreaseResultPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.StatIncreaseResultExtendedPlugIn_Name), Description = nameof(PlugInResources.StatIncreaseResultExtendedPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("4B9AEFF1-B139-45F9-9277-0FBDA7A3C020")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class StatIncreaseResultExtendedPlugIn : IStatIncreaseResultPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="StatIncreaseResultExtendedPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public StatIncreaseResultExtendedPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask StatIncreaseResultAsync(AttributeDefinition attribute, ushort addedPoints)
    {
        var connection = this._player.Connection;
        if (connection is null)
        {
            return;
        }

        await connection.SendCharacterStatIncreaseResponseExtendedAsync(
            attribute.GetStatType(),
            addedPoints,
            (uint)this._player.Attributes![Stats.MaximumHealth],
            (uint)this._player.Attributes![Stats.MaximumMana],
            (uint)this._player.Attributes![Stats.MaximumShield],
            (uint)this._player.Attributes![Stats.MaximumAbility]).ConfigureAwait(false);
    }
}