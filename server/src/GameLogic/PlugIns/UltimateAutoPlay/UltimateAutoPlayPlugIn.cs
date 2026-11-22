// <copyright file="UltimateAutoPlayPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.UltimateAutoPlay;

using System.Collections.Concurrent;
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
    IPeriodicTaskPlugIn,
    ISupportCustomConfiguration<UltimateAutoPlayConfiguration>,
    ISupportDefaultCustomConfiguration
{
    private static readonly TimeSpan VipCheckInterval = TimeSpan.FromSeconds(5);
    private readonly ConcurrentDictionary<Player, bool> _lastSentVip = new();
    private IGameContext? _gameContext;
    private long _nextVipCheckTicks;

    /// <inheritdoc />
    public UltimateAutoPlayConfiguration? Configuration { get; set; }

    /// <summary>Gets the active <see cref="UltimateAutoPlayPlugIn"/> for a game context, if enabled.</summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The plug-in, or <c>null</c> if it is not active.</returns>
    public static UltimateAutoPlayPlugIn? Get(IGameContext gameContext) =>
        gameContext.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<UltimateAutoPlayPlugIn>().FirstOrDefault();

    /// <summary>Determines whether <paramref name="player"/> currently has an active VIP grant or attribute.</summary>
    /// <param name="player">The player.</param>
    /// <returns><c>true</c> if VIP is active; otherwise, <c>false</c>.</returns>
    public static bool IsVip(Player player) =>
        (player.Attributes?[Stats.IsVip] ?? 0) > 0
        || (player.Account is { } account && account.GetAccountAttributeValue(Stats.IsVip, DateTime.UtcNow) > 0);

    /// <summary>Sends the resolved Ultimate Autoplay policy JSON to <paramref name="player"/>.</summary>
    /// <param name="player">The player.</param>
    /// <returns>The task.</returns>
    public static ValueTask SendPolicyAsync(Player player)
    {
        var plugin = Get(player.GameContext);
        return SendPolicyAsync(player, plugin, plugin is null ? null : plugin.Configuration ??= new UltimateAutoPlayConfiguration());
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new UltimateAutoPlayConfiguration();

    /// <inheritdoc />
    public ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (currentState == PlayerState.EnteredWorld && player.SelectedCharacter is not null)
        {
            this.WatchSettings(player.GameContext);
            return SendPolicyAsync(player, this, this.Configuration ??= new UltimateAutoPlayConfiguration());
        }

        if (currentState != PlayerState.EnteredWorld)
        {
            this._lastSentVip.TryRemove(player, out _);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var now = DateTime.UtcNow;
        if (now.Ticks < Interlocked.Read(ref this._nextVipCheckTicks))
        {
            return;
        }

        Interlocked.Exchange(ref this._nextVipCheckTicks, now.Add(VipCheckInterval).Ticks);
        var configuration = this.Configuration ??= new UltimateAutoPlayConfiguration();
        foreach (var player in await gameContext.GetPlayersAsync().ConfigureAwait(false))
        {
            if (player.SelectedCharacter is null
                || player.CurrentMap is null
                || player.PlayerState.CurrentState != PlayerState.EnteredWorld)
            {
                this._lastSentVip.TryRemove(player, out _);
                continue;
            }

            var isVip = IsVip(player);
            if (!this._lastSentVip.TryGetValue(player, out var lastVip) || lastVip != isVip)
            {
                this._lastSentVip[player] = isVip;
                await SendPolicyAsync(player, this, configuration, isVip).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public void ForceStart() => Interlocked.Exchange(ref this._nextVipCheckTicks, 0);

    private static ValueTask SendPolicyAsync(Player player, UltimateAutoPlayPlugIn? plugin, UltimateAutoPlayConfiguration? configuration, bool? knownVip = null)
    {
        // VIP on the character or on the account (VIP days), as the other VIP checks of HNMO read it.
        var isVip = knownVip ?? IsVip(player);
        if (plugin is not null)
        {
            plugin._lastSentVip[player] = isVip;
        }

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
                        await SendPolicyAsync(player, this, configuration).ConfigureAwait(false);
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
