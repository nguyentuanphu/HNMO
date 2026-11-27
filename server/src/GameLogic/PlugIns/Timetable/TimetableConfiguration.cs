// <copyright file="TimetableConfiguration.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Timetable;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Configuration for the <see cref="TimetablePlugIn"/> (OFF by default).
/// </summary>
public sealed class TimetableConfiguration
{
    /// <summary>Gets or sets the shared HNMO state file name.</summary>
    [Display(Name = "State file")]
    public string FileName { get; set; } = HnmoStateStore.StateFileName;

    /// <summary>Gets or sets the wall-clock schedule time zone identifier.</summary>
    [StringLength(80)]
    public string TimeZoneId { get; set; } = "Australia/Sydney";

    /// <summary>Gets or sets a value indicating whether active mini-game schedules are listed.</summary>
    public bool ShowMiniGames { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether active invasion schedules are listed.</summary>
    public bool ShowInvasions { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether active world bosses are listed.</summary>
    public bool ShowWorldBosses { get; set; } = true;

    /// <summary>Gets or sets host-defined custom timetable entries or overrides.</summary>
    public List<TimetableCustomEntry> CustomEntries { get; set; } = [];
}
