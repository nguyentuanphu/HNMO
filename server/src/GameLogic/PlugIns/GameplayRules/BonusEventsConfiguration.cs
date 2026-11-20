// <copyright file="BonusEventsConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GameplayRules;

/// <summary>
/// Configuration for <see cref="BonusEventsPlugIn"/> (ROADMAP 14.2). Off until enabled by host.
/// </summary>
public sealed class BonusEventsConfiguration
{
    /// <summary>Gets or sets a value indicating whether scheduled bonus events are enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets a value indicating whether start/end announcements are sent in chat.</summary>
    public bool AnnounceInChat { get; set; } = true;

    /// <summary>
    /// Gets or sets the time zone (IANA id, e.g. "Asia/Ho_Chi_Minh", or Windows id) the events' start times are in.
    /// Empty or unknown: Sydney.
    /// </summary>
    public string TimeZone { get; set; } = "Australia/Sydney";

    /// <summary>Gets or sets configured bonus event schedules.</summary>
    public List<BonusEventEntry> Events { get; set; } = [];
}
