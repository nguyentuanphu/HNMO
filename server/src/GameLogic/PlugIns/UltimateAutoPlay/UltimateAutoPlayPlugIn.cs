// <copyright file="UltimateAutoPlayPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.UltimateAutoPlay;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.Views;
using HNMO.PlugIns;

/// <summary>
/// Server plug-in that resolves and delivers Free vs VIP Ultimate Autoplay feature switches to web clients.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Ultimate Autoplay", Description = "Controls Free vs VIP access and switches for Ultimate Autoplay features.")]
[Guid("C4E7B1A2-8D3F-4E91-9A2B-7F5C1E3D8904")]
public sealed class UltimateAutoPlayPlugIn :
    IPlayerStateChangedPlugIn,
    ISupportCustomConfiguration<UltimateAutoPlayConfiguration>,
    ISupportDefaultCustomConfiguration
{
    /// <inheritdoc />
    public UltimateAutoPlayConfiguration? Configuration { get; set; }

    /// <summary>Gets the active <see cref="UltimateAutoPlayPlugIn"/> for a game context, if enabled.</summary>
    public static UltimateAutoPlayPlugIn? Get(IGameContext gameContext) =>
        gameContext.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<UltimateAutoPlayPlugIn>().FirstOrDefault();

    /// <inheritdoc />
    public object CreateDefaultConfig() => new UltimateAutoPlayConfiguration();

    /// <inheritdoc />
    public ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (currentState == PlayerState.EnteredWorld && player.SelectedCharacter is not null)
        {
            return SendPolicyAsync(player);
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>Sends the resolved Ultimate Autoplay policy JSON to <paramref name="player"/>.</summary>
    public static ValueTask SendPolicyAsync(Player player)
    {
        var plugin = Get(player.GameContext);
        var isVip = (player.Attributes?[Stats.IsVip] ?? 0) > 0;
        var json = plugin is null
            ? UltimateAutoPlayPolicyWriter.DisabledPolicyJson
            : UltimateAutoPlayPolicyWriter.Write(plugin.Configuration ??= new UltimateAutoPlayConfiguration(), plugInActive: true, isVip);
        return player.InvokeViewPlugInAsync<IHnmoUltimateAutoPlayViewPlugIn>(p => p.ShowUltimateAutoPlayPolicyAsync(json));
    }
}
