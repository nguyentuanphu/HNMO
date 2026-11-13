// <copyright file="ShowItemDropEffectPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Character;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Views.Character;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Pathfinding;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="ShowItemDropEffectPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowItemDropEffectPlugIn_Name), Description = nameof(PlugInResources.ShowItemDropEffectPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("AA949D5E-0CC6-424E-8412-DE12EA294E33")]
public class ShowItemDropEffectPlugIn : IShowItemDropEffectPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowItemDropEffectPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowItemDropEffectPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowEffectAsync(ItemDropEffect effect, Point targetCoordinates)
    {
        var (x, y) = targetCoordinates;
        switch (effect)
        {
            case ItemDropEffect.Fireworks:
                await this._player.Connection.SendShowFireworksAsync(x, y).ConfigureAwait(false);
                break;
            case ItemDropEffect.ChristmasFireworks:
                await this._player.Connection.SendShowChristmasFireworksAsync(x, y).ConfigureAwait(false);
                break;
            case ItemDropEffect.FanfareSound:
                await this._player.Connection.SendPlayFanfareSoundAsync(x, y).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(effect));
        }
    }
}