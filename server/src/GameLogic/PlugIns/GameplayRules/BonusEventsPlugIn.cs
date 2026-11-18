// <copyright file="BonusEventsPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GameplayRules;

using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.PeriodicTasks;
using HNMO.GameLogic.Views;
using HNMO.Interfaces;
using HNMO.PlugIns;

/// <summary>
/// Scheduled per-map and server-wide bonus events ("happy hours") evaluated in Sydney time,
/// with EXP/item/zen bonus multipliers, EN/VI chat notices, and event timetable entries (ROADMAP 14.2).
/// </summary>
[PlugIn]
[Display(Name = "Bonus events (happy hours)", Description = "Scheduled Sydney-time EXP, item drop, and zen bonuses per map or server-wide with chat announcements.")]
[Guid("7F3B4D92-1E5C-4B8A-9C64-4E2A9D1F7B50")]
public sealed class BonusEventsPlugIn :
    IFeaturePlugIn,
    IPeriodicTaskPlugIn,
    IExperienceCalculationPlugIn,
    ISupportCustomConfiguration<BonusEventsConfiguration>,
    ISupportDefaultCustomConfiguration
{
    private static readonly TimeZoneInfo SydneyZone = ResolveSydneyZone();
    private readonly ConcurrentDictionary<IGameContext, HashSet<string>> _activeAnnouncements = new();

    /// <summary>Gets or sets the optional clock override (primarily for tests).</summary>
    public TimeProvider? TimeProviderOverride { get; set; }

    /// <inheritdoc />
    public BonusEventsConfiguration? Configuration { get; set; }

    /// <summary>Gets the active <see cref="BonusEventsPlugIn"/> for <paramref name="gameContext"/>.</summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The active plug-in, or <c>null</c>.</returns>
    public static BonusEventsPlugIn? Get(IGameContext? gameContext) =>
        gameContext?.FeaturePlugIns.GetPlugIn<BonusEventsPlugIn>()
        ?? gameContext?.PlugInManager.GetActivePlugInsOf<IPeriodicTaskPlugIn>().OfType<BonusEventsPlugIn>().FirstOrDefault();

    /// <summary>Evaluates whether <paramref name="ev"/> is active at <paramref name="utcNow"/> (Sydney local time).</summary>
    /// <param name="ev">Event rule.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <param name="mapNumber">Optional map filter (<c>null</c> checks any map).</param>
    /// <returns><c>true</c> if active.</returns>
    public static bool IsWindowActive(BonusEventEntry ev, DateTime utcNow, short? mapNumber = null)
    {
        ArgumentNullException.ThrowIfNull(ev);
        if (!ev.Enabled)
        {
            return false;
        }

        if (mapNumber is { } targetMap && ev.MapNumber is { } ruleMap && ruleMap != targetMap)
        {
            return false;
        }

        if (!TimeOnly.TryParseExact(ev.StartTimeSydney?.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
        {
            return false;
        }

        var duration = TimeSpan.FromMinutes(Math.Clamp(ev.DurationMinutes, 1, 1440));
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), SydneyZone);
        var todayStart = localNow.Date + start.ToTimeSpan();
        var yesterdayStart = todayStart.AddDays(-1);
        return (localNow >= todayStart && localNow < todayStart + duration)
            || (localNow >= yesterdayStart && localNow < yesterdayStart + duration);
    }

    /// <summary>Gets combined active <c>(ExpMultiplier, ItemDropMultiplier, ZenDropMultiplier)</c> for <paramref name="player"/>.</summary>
    /// <param name="player">The player.</param>
    /// <param name="utcNow">UTC timestamp.</param>
    /// <returns>Combined multipliers.</returns>
    public static (double ExpMultiplier, double ItemDropMultiplier, double ZenDropMultiplier) GetDropModifiers(
        Player? player,
        DateTime utcNow)
    {
        if (player?.CurrentMap?.Definition is not { } map)
        {
            return (1.0, 1.0, 1.0);
        }

        var plugIn = Get(player.GameContext);
        if (plugIn is null)
        {
            return (1.0, 1.0, 1.0);
        }

        return plugIn.GetCombinedModifiers(map.Number, utcNow);
    }

    /// <summary>Computes next Sydney start timestamp (UTC) for an event entry.</summary>
    /// <param name="ev">Event entry.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>Next start UTC timestamp, or <c>null</c> if disabled/invalid.</returns>
    public static DateTime? NextStartUtc(BonusEventEntry ev, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(ev);
        if (!ev.Enabled || !TimeOnly.TryParseExact(ev.StartTimeSydney?.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
        {
            return null;
        }

        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), SydneyZone);
        var candidate = localNow.Date + start.ToTimeSpan();
        if (candidate <= localNow)
        {
            candidate = candidate.AddDays(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(candidate, DateTimeKind.Unspecified), SydneyZone);
    }

    /// <summary>Gets active multipliers for <paramref name="mapNumber"/> at <paramref name="utcNow"/>.</summary>
    /// <param name="mapNumber">Target map number.</param>
    /// <param name="utcNow">Current UTC time.</param>
    /// <returns>Multipliers.</returns>
    public (double ExpMultiplier, double ItemDropMultiplier, double ZenDropMultiplier) GetCombinedModifiers(short mapNumber, DateTime utcNow)
    {
        if (this.Configuration is not { Enabled: true, Events.Count: > 0 } cfg)
        {
            return (1.0, 1.0, 1.0);
        }

        var expBonus = 0;
        var itemBonus = 0;
        var zenBonus = 0;
        foreach (var ev in cfg.Events)
        {
            if (IsWindowActive(ev, utcNow, mapNumber))
            {
                expBonus += ev.ExpBonusPercent;
                itemBonus += ev.ItemDropBonusPercent;
                zenBonus += ev.ZenDropBonusPercent;
            }
        }

        return (
            MapRulesPlugIn.PercentToMultiplier(expBonus),
            MapRulesPlugIn.PercentToMultiplier(itemBonus),
            MapRulesPlugIn.PercentToMultiplier(zenBonus));
    }

    /// <summary>Returns timetable summary rows (name, active flag, Sydney time, bonuses) for Event Timetable integration (13.6).</summary>
    /// <param name="utcNow">Current UTC time.</param>
    /// <returns>Timetable rows.</returns>
    public IReadOnlyList<(string Id, string NameEn, string NameVi, short? MapNumber, bool IsActive, DateTime? NextStartUtc, int ExpBonusPercent, int ItemDropBonusPercent, int ZenDropBonusPercent)> GetTimetableEntries(DateTime utcNow)
    {
        if (this.Configuration is not { Enabled: true, Events.Count: > 0 } cfg)
        {
            return [];
        }

        return cfg.Events
            .Where(e => e.Enabled)
            .Select(e => (
                e.Id,
                e.NameEn,
                e.NameVi,
                e.MapNumber,
                IsWindowActive(e, utcNow),
                NextStartUtc(e, utcNow),
                e.ExpBonusPercent,
                e.ItemDropBonusPercent,
                e.ZenDropBonusPercent))
            .ToList();
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new BonusEventsConfiguration();

    /// <inheritdoc />
    public void ForceStart() => this._activeAnnouncements.Clear();

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        if (this.Configuration is not { Enabled: true, Events.Count: > 0 } cfg)
        {
            return;
        }

        var now = (this.TimeProviderOverride ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        var currentActive = cfg.Events.Where(e => IsWindowActive(e, now)).ToList();
        var prevSet = this._activeAnnouncements.GetOrAdd(gameContext, _ => []);

        List<BonusEventEntry> started;
        List<BonusEventEntry> ended;
        lock (prevSet)
        {
            var nextIds = currentActive.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
            started = currentActive.Where(e => !prevSet.Contains(e.Id)).ToList();
            ended = cfg.Events.Where(e => prevSet.Contains(e.Id) && !nextIds.Contains(e.Id)).ToList();
            prevSet.Clear();
            foreach (var id in nextIds)
            {
                prevSet.Add(id);
            }
        }

        if (!cfg.AnnounceInChat || (started.Count == 0 && ended.Count == 0))
        {
            return;
        }

        await gameContext.ForEachPlayerAsync(async player =>
        {
            var isVi = player.Culture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase)
                || (player.Account?.LanguageIsoCode?.StartsWith("vi", StringComparison.OrdinalIgnoreCase) ?? false);
            foreach (var ev in started)
            {
                var scopeEn = ev.MapNumber is { } m ? $"Map {m}" : "All maps";
                var scopeVi = ev.MapNumber is { } m2 ? $"Bản đồ {m2}" : "Toàn máy chủ";
                var msg = isVi
                    ? $"[Sự kiện] {ev.NameVi} ({scopeVi}) đã bắt đầu: +{ev.ExpBonusPercent}% EXP, +{ev.ItemDropBonusPercent}% Rớt đồ, +{ev.ZenDropBonusPercent}% Zen!"
                    : $"[Bonus Event] {ev.NameEn} ({scopeEn}) has started: +{ev.ExpBonusPercent}% EXP, +{ev.ItemDropBonusPercent}% Item Drop, +{ev.ZenDropBonusPercent}% Zen!";
                await player.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(msg, MessageType.GoldenCenter)).ConfigureAwait(false);
            }

            foreach (var ev in ended)
            {
                var msg = isVi
                    ? $"[Sự kiện] {ev.NameVi} đã kết thúc."
                    : $"[Bonus Event] {ev.NameEn} has ended.";
                await player.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(msg, MessageType.GoldenCenter)).ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask CalculateExperienceAsync(Player player, ExperienceCalculationArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (player.CurrentMap?.Definition is not { } map)
        {
            return ValueTask.CompletedTask;
        }

        var (expMult, _, _) = this.GetCombinedModifiers(map.Number, (this.TimeProviderOverride ?? TimeProvider.System).GetUtcNow().UtcDateTime);
        if (Math.Abs(expMult - 1.0) > 0.0001)
        {
            args.Experience *= expMult;
        }

        return ValueTask.CompletedTask;
    }

    private static TimeZoneInfo ResolveSydneyZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time");
        }
    }
}
