// <copyright file="TimetableCustomEntry.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Timetable;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// A host-defined custom entry or drop override shown on the event and boss timetable.
/// </summary>
public sealed class TimetableCustomEntry
{
    /// <summary>Gets or sets the unique key of the entry.</summary>
    [StringLength(48)]
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether this entry is visible.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the category: <c>event</c>, <c>invasion</c>, <c>boss</c>, or <c>custom</c>.</summary>
    [StringLength(16)]
    public string Category { get; set; } = "custom";

    /// <summary>Gets or sets the English name.</summary>
    [StringLength(80)]
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese name.</summary>
    [StringLength(80)]
    public string NameVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the English map description.</summary>
    [StringLength(120)]
    public string MapsEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese map description.</summary>
    [StringLength(120)]
    public string MapsVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the English drop/reward summary.</summary>
    [StringLength(240)]
    public string DropsEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese drop/reward summary.</summary>
    [StringLength(240)]
    public string DropsVi { get; set; } = string.Empty;

    /// <summary>Gets or sets comma-separated daily times in the configured server time zone (e.g. "00:30, 12:30, 20:30").</summary>
    [StringLength(240)]
    public string ScheduleTimes { get; set; } = "20:00";

    /// <summary>Gets or sets the active duration in minutes.</summary>
    [Range(1, 720)]
    public int DurationMinutes { get; set; } = 30;
}
