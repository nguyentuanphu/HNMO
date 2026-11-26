// <copyright file="S9InvasionScheduleEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.SeasonNineEvents;

using System.Collections.Generic;

/// <summary>
/// One configurable scheduled invasion group managed by the Season 9 Invasion Manager.
/// </summary>
public sealed class S9InvasionScheduleEntry
{
    /// <summary>Gets or sets unique invasion identifier (e.g. <c>red-dragon</c>, <c>s9-moon-rabbit</c>).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets English display name.</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Gets or sets Vietnamese display name.</summary>
    public string NameVi { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether this invasion track is active.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets primary monster number spawned by this invasion.</summary>
    public short MonsterNumber { get; set; }

    /// <summary>Gets or sets monster spawn count per target map.</summary>
    public ushort MobCount { get; set; } = 10;

    /// <summary>Gets or sets target map numbers where mobs appear.</summary>
    public List<short> MapNumbers { get; set; } = [];

    /// <summary>Gets or sets daily start times (<c>HH:mm</c>) in configured timezone.</summary>
    public List<string> DailyTimes { get; set; } = [];

    /// <summary>Gets or sets optional day-of-week filter (<c>-1</c> = every day, <c>0</c>=Sun .. <c>6</c>=Sat).</summary>
    public int DayOfWeek { get; set; } = -1;

    /// <summary>Gets or sets invasion active window duration in minutes.</summary>
    public int DurationMinutes { get; set; } = 15;

    /// <summary>Gets or sets a value indicating whether deaths are broadcast globally.</summary>
    public bool AnnounceDeath { get; set; } = true;
}
