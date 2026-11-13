// <copyright file="WeatherStatusUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.World;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views.World;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IWeatherStatusUpdatePlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.WeatherStatusUpdatePlugIn_Name), Description = nameof(PlugInResources.WeatherStatusUpdatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("44369927-4EE4-47D7-9C6C-DD74FC824071")]
public class WeatherStatusUpdatePlugIn : IWeatherStatusUpdatePlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeatherStatusUpdatePlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public WeatherStatusUpdatePlugIn(RemotePlayer player)
    {
        this._player = player;
    }

    /// <inheritdoc />
    public ValueTask ShowWeatherAsync(byte weather, byte variation)
    {
        return this._player.Connection.SendWeatherStatusUpdateAsync(weather, variation);
    }
}