// <copyright file="TimetableSourceItem.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Timetable;

using System.Globalization;
using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.InvasionEvents;
using HNMO.GameLogic.PlugIns.PeriodicTasks;
using HNMO.GameLogic.PlugIns.WorldBosses;

/// <summary>
/// Describes a scheduled source item (mini-game, invasion, world boss, or custom event) for <see cref="TimetableEngine"/>.
/// </summary>
public sealed class TimetableSourceItem
{
    /// <summary>Gets or sets the unique key.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the category (<c>event</c>, <c>invasion</c>, <c>boss</c>, or <c>custom</c>).</summary>
    public string Category { get; set; } = "event";

    /// <summary>Gets or sets the English name.</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese name.</summary>
    public string NameVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the English map names.</summary>
    public string MapsEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese map names.</summary>
    public string MapsVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the English drop/reward summary.</summary>
    public string DropsEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese drop/reward summary.</summary>
    public string DropsVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the daily schedule times (wall-clock in server time zone).</summary>
    public IReadOnlyList<TimeOnly> DailyTimes { get; set; } = [];

    /// <summary>Gets or sets optional allowed days of the week (empty means every day).</summary>
    public IReadOnlyList<DayOfWeek> AllowedDays { get; set; } = [];

    /// <summary>Gets or sets the duration of each occurrence.</summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(20);

    /// <summary>Gets or sets an optional precomputed next start time in UTC.</summary>
    public DateTime? ExplicitNextStartUtc { get; set; }

    /// <summary>Gets or sets an optional active end time in UTC if the event is currently running.</summary>
    public DateTime? ExplicitEndsAtUtc { get; set; }

    /// <summary>Gets or sets a value indicating whether the source is currently live on a server.</summary>
    public bool IsLiveNow { get; set; }

    /// <summary>Gets or sets the effective invasion spawn rules for a configured invasion.</summary>
    [JsonIgnore]
    public PeriodicInvasionConfiguration? InvasionConfiguration { get; set; }

    /// <summary>Gets or sets the active spawn rules of a running invasion plug-in.</summary>
    [JsonIgnore]
    public IReadOnlyList<InvasionSpawnConfiguration>? ActiveInvasionSpawns { get; set; }

    /// <summary>Gets or sets the effective boss definition for a configured boss.</summary>
    [JsonIgnore]
    public WorldBossDefinition? BossConfiguration { get; set; }

    /// <summary>Gets or sets the configured prize for an event with one winner.</summary>
    [JsonIgnore]
    public (int Coins, CashShopCoinType CoinType, long Zen)? WinnerReward { get; set; }
}
