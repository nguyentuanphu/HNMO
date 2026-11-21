// <copyright file="UltimateAutoPlayPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.UltimateAutoPlay;

using System.Runtime.InteropServices;
using System.Threading;
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
    private IGameContext? _gameContext;

    /// <inheritdoc />
    public UltimateAutoPlayConfiguration? Configuration { get; set; }

    /// <summary>Gets the active <see cref="UltimateAutoPlayPlugIn"/> for a game context, if enabled.</summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The plug-in, or <c>null</c> if it is not active.</returns>
    public static UltimateAutoPlayPlugIn? Get(IGameContext gameContext) =>
        gameContext.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<UltimateAutoPlayPlugIn>().FirstOrDefault();

    /// <summary>Sends the resolved Ultimate Autoplay policy JSON to <paramref name="player"/>.</summary>
    /// <param name="player">The player.</param>
    /// <returns>The task.</returns>
    public static ValueTask SendPolicyAsync(Player player)
    {
        var plugin = Get(player.GameContext);
        return SendPolicyAsync(player, plugin is null ? null : plugin.Configuration ??= new UltimateAutoPlayConfiguration());
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new UltimateAutoPlayConfiguration();

    /// <inheritdoc />
    public ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (currentState == PlayerState.EnteredWorld && player.SelectedCharacter is not null)
        {
            this.WatchSettings(player.GameContext);
            return SendPolicyAsync(player);
        }

        return ValueTask.CompletedTask;
    }

    private static ValueTask SendPolicyAsync(Player player, UltimateAutoPlayConfiguration? configuration)
    {
        // VIP on the character or on the account (VIP days), as the other VIP checks of HNMO read it.
        var isVip = (player.Attributes?[Stats.IsVip] ?? 0) > 0
            || (player.Account is { } account && account.GetAccountAttributeValue(Stats.IsVip, DateTime.UtcNow) > 0);
        var json = configuration is null
            ? UltimateAutoPlayPolicyWriter.DisabledPolicyJson
            : UltimateAutoPlayPolicyWriter.Write(configuration, plugInActive: true, isVip);
        return player.InvokeViewPlugInAsync<IHnmoUltimateAutoPlayViewPlugIn>(p => p.ShowUltimateAutoPlayPolicyAsync(json));
    }

    /// <summary>
    /// Once per game server: when the admin panel saves the settings, switches the plug-in on or off, every player in
    /// the game gets the new policy at once (not only at the next world entry or Alt+Z).
    /// </summary>
    private void WatchSettings(IGameContext gameContext)
    {
        if (Interlocked.CompareExchange(ref this._gameContext, gameContext, null) is not null)
        {
            return;
        }

        gameContext.PlugInManager.PlugInConfigurationChanged += this.OnSettingsChanged;
        gameContext.PlugInManager.PlugInActivated += this.OnSwitched;
        gameContext.PlugInManager.PlugInDeactivated += this.OnSwitched;
    }

    private void OnSettingsChanged(object? sender, PlugInConfigurationChangedEventArgs e)
    {
        if (e.PlugInType == typeof(UltimateAutoPlayPlugIn) && e.Configuration.IsActive)
        {
            this.SendToEveryone(e.Configuration.GetConfiguration<UltimateAutoPlayConfiguration>(null) ?? new UltimateAutoPlayConfiguration());
        }
    }

    private void OnSwitched(object? sender, PlugInEventArgs e)
    {
        if (e.PlugInType == typeof(UltimateAutoPlayPlugIn) && this._gameContext is { } gameContext)
        {
            this.SendToEveryone(Get(gameContext) is { } plugin ? plugin.Configuration ?? new UltimateAutoPlayConfiguration() : null);
        }
    }

    private void SendToEveryone(UltimateAutoPlayConfiguration? configuration)
    {
        if (this._gameContext is not { } gameContext)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await gameContext.ForEachPlayerAsync(async player =>
                {
                    if (player.SelectedCharacter is not null && player.CurrentMap is not null)
                    {
                        await SendPolicyAsync(player, configuration).ConfigureAwait(false);
                    }
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                gameContext.LoggerFactory.CreateLogger<UltimateAutoPlayPlugIn>().LogWarning(ex, "Couldn't send the new Ultimate Autoplay settings to the players.");
            }
        });
    }
}
