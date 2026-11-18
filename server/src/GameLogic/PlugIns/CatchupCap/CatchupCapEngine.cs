// <copyright file="CatchupCapEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.CatchupCap;

using System.Globalization;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// Computed Catch-up Level/Reset Cap snapshot for a specific timestamp.
/// </summary>
/// <param name="LevelCapEnabled">Whether level cap mode is active.</param>
/// <param name="TodayLevelCap">Current level cap for all players.</param>
/// <param name="InitialLevelCap">Initial cap / trigger level.</param>
/// <param name="HardMaxLevelCap">Absolute ceiling level.</param>
/// <param name="LevelTriggered">Whether the first player has reached the initial cap.</param>
/// <param name="LevelFirstPlayer">Name of first player who reached initial cap.</param>
/// <param name="ResetCapEnabled">Whether reset daily cap mode is active.</param>
/// <param name="TodayDailyResetCap">Current daily reset cap.</param>
/// <param name="ResetTriggered">Whether the reset growth trigger has fired.</param>
/// <param name="ResetFirstPlayer">Name of first player who reached trigger resets.</param>
/// <param name="NextDayStartsAtUtc">UTC timestamp of next Sydney day tick when caps increment.</param>
public sealed record CatchupCapSnapshot(
    bool LevelCapEnabled,
    int TodayLevelCap,
    int InitialLevelCap,
    int HardMaxLevelCap,
    bool LevelTriggered,
    string LevelFirstPlayer,
    bool ResetCapEnabled,
    int TodayDailyResetCap,
    bool ResetTriggered,
    string ResetFirstPlayer,
    DateTime NextDayStartsAtUtc);

/// <summary>
/// Pure clock-driven engine computing today's Catch-up Level Cap and Daily Reset Cap in Sydney time.
/// </summary>
public sealed class CatchupCapEngine
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CatchupCapEngine"/> class.
    /// </summary>
    /// <param name="configuration">Configuration rules.</param>
    /// <param name="store">Persistent store.</param>
    /// <param name="enabled">Whether the plug-in is active.</param>
    public CatchupCapEngine(CatchupCapConfiguration configuration, CatchupCapStore store, bool enabled)
    {
        this.Configuration = configuration;
        this.Store = store;
        this.Enabled = enabled;
        this.Clock = new MissionClock(configuration.TimeZone, configuration.DayStartHour);
    }

    /// <summary>Gets the configuration.</summary>
    public CatchupCapConfiguration Configuration { get; }

    /// <summary>Gets the atomic store.</summary>
    public CatchupCapStore Store { get; }

    /// <summary>Gets a value indicating whether the feature is active.</summary>
    public bool Enabled { get; }

    /// <summary>Gets the Sydney-aware day clock.</summary>
    public MissionClock Clock { get; }

    /// <summary>Observes a player's current level; fires trigger once when the first player reaches <see cref="CatchupCapConfiguration.InitialLevelCap"/>.</summary>
    /// <param name="characterName">Character name.</param>
    /// <param name="level">Current level.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns><c>true</c> if this observation newly triggered the catch-up progression.</returns>
    public bool ObserveLevel(string characterName, int level, DateTime utcNow)
    {
        if (!this.Enabled || !this.Configuration.LevelCapEnabled || level < this.Configuration.InitialLevelCap)
        {
            return false;
        }

        var now = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        var dayKey = this.Clock.DayKey(now);
        return this.Store.Mutate(data =>
        {
            if (data.LevelTriggeredAtUtc is not null)
            {
                return (false, false);
            }

            data.LevelTriggeredAtUtc = now;
            data.LevelFirstPlayer = characterName;
            data.LevelTriggerDayKey = dayKey;
            return (true, true);
        });
    }

    /// <summary>Observes a player's reset count; fires trigger once when the first player reaches <see cref="CatchupCapConfiguration.TriggerResets"/>.</summary>
    /// <param name="characterName">Character name.</param>
    /// <param name="resets">Current reset count.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns><c>true</c> if this observation newly triggered reset growth.</returns>
    public bool ObserveResets(string characterName, int resets, DateTime utcNow)
    {
        if (!this.Enabled || !this.Configuration.ResetCapEnabled || resets < this.Configuration.TriggerResets)
        {
            return false;
        }

        var now = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        var dayKey = this.Clock.DayKey(now);
        return this.Store.Mutate(data =>
        {
            if (data.ResetTriggeredAtUtc is not null)
            {
                return (false, false);
            }

            data.ResetTriggeredAtUtc = now;
            data.ResetFirstPlayer = characterName;
            data.ResetTriggerDayKey = dayKey;
            return (true, true);
        });
    }

    /// <summary>Computes the effective level and daily reset caps for <paramref name="utcNow"/>.</summary>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>Computed snapshot.</returns>
    public CatchupCapSnapshot GetSnapshot(DateTime utcNow)
    {
        var now = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        var todayKey = this.Clock.DayKey(now);
        var nextDayUtc = this.Clock.NextDayStart(now);
        var state = this.Store.Read(d => (d.LevelTriggerDayKey, d.LevelTriggeredAtUtc, d.LevelFirstPlayer, d.ResetTriggerDayKey, d.ResetTriggeredAtUtc, d.ResetFirstPlayer));

        var cfg = this.Configuration;
        var hardMaxLevel = Math.Max(cfg.InitialLevelCap, cfg.HardMaxLevelCap);
        var todayLevel = cfg.InitialLevelCap;
        var levelTriggered = state.LevelTriggeredAtUtc is not null;
        if (levelTriggered && !string.IsNullOrEmpty(state.LevelTriggerDayKey))
        {
            var elapsedDays = DaysBetween(state.LevelTriggerDayKey, todayKey);
            todayLevel = (int)Math.Min(hardMaxLevel, (long)cfg.InitialLevelCap + (long)elapsedDays * Math.Max(0, cfg.LevelsPerDay));
        }

        var resetTriggered = state.ResetTriggeredAtUtc is not null || cfg.TriggerResets == 0;
        var triggerResetDay = !string.IsNullOrEmpty(state.ResetTriggerDayKey) ? state.ResetTriggerDayKey : todayKey;
        var todayResets = cfg.InitialDailyResets;
        if (resetTriggered && !string.IsNullOrEmpty(triggerResetDay))
        {
            var elapsedResetDays = DaysBetween(triggerResetDay, todayKey);
            var grown = (long)cfg.InitialDailyResets + (long)elapsedResetDays * Math.Max(0, cfg.DailyResetsPerDay);
            todayResets = cfg.HardMaxDailyResets > 0 ? (int)Math.Min(cfg.HardMaxDailyResets, grown) : (int)Math.Min(int.MaxValue, grown);
        }

        return new CatchupCapSnapshot(
            LevelCapEnabled: this.Enabled && cfg.LevelCapEnabled,
            TodayLevelCap: todayLevel,
            InitialLevelCap: cfg.InitialLevelCap,
            HardMaxLevelCap: hardMaxLevel,
            LevelTriggered: levelTriggered,
            LevelFirstPlayer: state.LevelFirstPlayer,
            ResetCapEnabled: this.Enabled && cfg.ResetCapEnabled,
            TodayDailyResetCap: todayResets,
            ResetTriggered: resetTriggered,
            ResetFirstPlayer: state.ResetFirstPlayer,
            NextDayStartsAtUtc: nextDayUtc);
    }

    private static int DaysBetween(string startDayKey, string endDayKey)
    {
        if (!DateTime.TryParseExact(startDayKey, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)
            || !DateTime.TryParseExact(endDayKey, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
        {
            return 0;
        }

        return Math.Max(0, (int)(end - start).TotalDays);
    }
}
