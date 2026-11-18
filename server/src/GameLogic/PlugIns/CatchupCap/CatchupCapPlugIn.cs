// <copyright file="CatchupCapPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.CatchupCap;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns.PeriodicTasks;
using HNMO.GameLogic.Views;
using HNMO.Interfaces;
using HNMO.PlugIns;

/// <summary>
/// Catch-up level cap &amp; daily reset limit progression plug-in (OFF by default).
/// Once the first player reaches the initial cap, today's cap rises by N levels (or resets/day) every Sydney day.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Catch-up Level Cap", Description = "Rises daily level cap or daily reset cap by N/day after the first player reaches the cap (OFF by default).")]
[Guid("6C9D8B41-3E7F-4D2A-B8A0-7E4C9A1B5D20")]
public sealed class CatchupCapPlugIn :
    IPeriodicTaskPlugIn,
    IPlayerStateChangedPlugIn,
    ICharacterLevelUpPlugIn,
    ISupportCustomConfiguration<CatchupCapConfiguration>,
    ISupportDefaultCustomConfiguration,
    IDisabledByDefault
{
    private (CatchupCapConfiguration Config, CatchupCapEngine Engine)? _engine;
    private string _lastBroadcastDayKey = string.Empty;

    /// <inheritdoc />
    public CatchupCapConfiguration? Configuration { get; set; }

    /// <summary>Gets the active <see cref="CatchupCapPlugIn"/> for a game context, or <c>null</c> when disabled.</summary>
    /// <param name="gameContext">Game context.</param>
    /// <returns>Active plug-in or <c>null</c>.</returns>
    public static CatchupCapPlugIn? Get(IGameContext gameContext) =>
        gameContext.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<CatchupCapPlugIn>().FirstOrDefault();

    /// <summary>Returns the effective maximum character level taking Catch-up Level Cap into account when active.</summary>
    /// <param name="gameContext">Game context.</param>
    /// <param name="utcNow">Optional UTC timestamp.</param>
    /// <returns>Effective level cap.</returns>
    public static int GetEffectiveMaximumLevel(IGameContext gameContext, DateTime? utcNow = null)
    {
        var serverMax = (int)gameContext.Configuration.MaximumLevel;
        if (serverMax <= 0 || Get(gameContext) is not { } plugIn)
        {
            return serverMax;
        }

        var snap = plugIn.GetEngine().GetSnapshot(utcNow ?? DateTime.UtcNow);
        if (!snap.LevelCapEnabled)
        {
            return serverMax;
        }

        return Math.Clamp(snap.TodayLevelCap, 1, serverMax);
    }

    /// <summary>Returns today's effective daily reset limit if reset catch-up mode is active, or <c>null</c> if unrestricted by this plug-in.</summary>
    /// <param name="gameContext">Game context.</param>
    /// <param name="utcNow">UTC timestamp.</param>
    /// <returns>Daily reset cap, or <c>null</c>.</returns>
    public static int? GetEffectiveDailyResetCap(IGameContext gameContext, DateTime? utcNow = null)
    {
        if (Get(gameContext) is not { } plugIn)
        {
            return null;
        }

        var snap = plugIn.GetEngine().GetSnapshot(utcNow ?? DateTime.UtcNow);
        return snap.ResetCapEnabled ? snap.TodayDailyResetCap : null;
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new CatchupCapConfiguration();

    /// <summary>Gets or creates the engine for the current configuration.</summary>
    /// <returns>Engine instance.</returns>
    public CatchupCapEngine GetEngine()
    {
        var cfg = this.Configuration ??= new CatchupCapConfiguration();
        if (this._engine is { } cached && ReferenceEquals(cached.Config, cfg))
        {
            return cached.Engine;
        }

        var engine = new CatchupCapEngine(cfg, CatchupCapStore.Open(cfg.FileName), enabled: true);
        this._engine = (cfg, engine);
        return engine;
    }

    /// <inheritdoc />
    public void ForceStart()
    {
    }

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var engine = this.GetEngine();
        var now = DateTime.UtcNow;
        var dayKey = engine.Clock.DayKey(now);
        if (!string.IsNullOrEmpty(this._lastBroadcastDayKey) && this._lastBroadcastDayKey != dayKey)
        {
            this._lastBroadcastDayKey = dayKey;
            var json = CatchupCapStateWriter.WritePublic(engine, now);
            await gameContext.ForEachPlayerAsync(async p =>
            {
                await p.InvokeViewPlugInAsync<IHnmoCatchupCapViewPlugIn>(v => v.ShowCatchupCapAsync(json, requested: false)).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        this._lastBroadcastDayKey = dayKey;
    }

    /// <inheritdoc />
    public ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (currentState == PlayerState.EnteredWorld && player.SelectedCharacter is { } ch)
        {
            var engine = this.GetEngine();
            var level = (int)(player.Attributes?[Stats.Level] ?? 1);
            var resets = (int)(player.Attributes?[Stats.Resets] ?? 0);
            engine.ObserveLevel(ch.Name, level, DateTime.UtcNow);
            engine.ObserveResets(ch.Name, resets, DateTime.UtcNow);
            return this.SendStateAsync(player, requested: false);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void CharacterLeveledUp(Player player)
    {
        if (player.SelectedCharacter is not { } ch || player.Attributes is null)
        {
            return;
        }

        var engine = this.GetEngine();
        var now = DateTime.UtcNow;
        var level = (int)player.Attributes[Stats.Level];
        if (engine.ObserveLevel(ch.Name, level, now) && engine.Configuration.AnnounceInGame)
        {
            var snap = engine.GetSnapshot(now);
            _ = this.BroadcastAnnouncementAsync(
                player.GameContext,
                $"Catch-up cap unlocked by {ch.Name} (Lv.{snap.InitialLevelCap})! Cap rises +{engine.Configuration.LevelsPerDay} levels/day.",
                $"{ch.Name} đã chạm mốc cấp {snap.InitialLevelCap}! Giới hạn cấp server sẽ tăng +{engine.Configuration.LevelsPerDay} mỗi ngày.");
        }
    }

    /// <summary>Sends today's Catch-up Cap JSON to a player.</summary>
    /// <param name="player">Target player.</param>
    /// <param name="requested">Whether requested by client.</param>
    /// <returns>A task representing the send.</returns>
    public ValueTask SendStateAsync(Player player, bool requested)
    {
        var json = CatchupCapStateWriter.WritePublic(this.GetEngine(), DateTime.UtcNow);
        return player.InvokeViewPlugInAsync<IHnmoCatchupCapViewPlugIn>(p => p.ShowCatchupCapAsync(json, requested));
    }

    private async Task BroadcastAnnouncementAsync(IGameContext gameContext, string en, string vi)
    {
        var json = CatchupCapStateWriter.WritePublic(this.GetEngine(), DateTime.UtcNow);
        await gameContext.ForEachPlayerAsync(async p =>
        {
            var isVi = string.Equals(p.Culture.TwoLetterISOLanguageName, "vi", StringComparison.OrdinalIgnoreCase);
            await p.InvokeViewPlugInAsync<IShowMessagePlugIn>(v => v.ShowMessageAsync(isVi ? vi : en, MessageType.GoldenCenter)).ConfigureAwait(false);
            await p.InvokeViewPlugInAsync<IHnmoCatchupCapViewPlugIn>(v => v.ShowCatchupCapAsync(json, requested: false)).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }
}
