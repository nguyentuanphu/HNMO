// <copyright file="ShowMoneyDropExtendedPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.World;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views.World;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.Pathfinding;
using HNMO.PlugIns;

/// <summary>
/// The extended implementation of the <see cref="IShowDroppedItemsPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowMoneyDropExtendedPlugIn_Name), Description = nameof(PlugInResources.ShowMoneyDropExtendedPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("292D399E-3F48-4AF0-9480-F83267BB8619")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class ShowMoneyDropExtendedPlugIn : IShowMoneyDropPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowMoneyDropExtendedPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowMoneyDropExtendedPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public ValueTask ShowMoneyAsync(ushort itemId, bool isFreshDrop, uint amount, Point point)
    {
        return this._player.Connection.SendMoneyDroppedExtendedAsync(isFreshDrop, itemId, point.X, point.Y, amount);
    }
}