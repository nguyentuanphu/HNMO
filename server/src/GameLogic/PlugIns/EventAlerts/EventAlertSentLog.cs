// <copyright file="EventAlertSentLog.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.EventAlerts;

/// <summary>
/// Audit record of an event alert dispatch batch (Web Push + Discord).
/// </summary>
public sealed class EventAlertSentLog
{
    /// <summary>Gets or sets the deduplication key (<c>id:epochMinute:phase</c>).</summary>
    public string SentKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the English alert title.</summary>
    public string TitleEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese alert title.</summary>
    public string TitleVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC timestamp when dispatched.</summary>
    public DateTime SentAtUtc { get; set; }

    /// <summary>Gets or sets how many Web Push deliveries succeeded.</summary>
    public int PushSucceeded { get; set; }

    /// <summary>Gets or sets how many Web Push deliveries failed.</summary>
    public int PushFailed { get; set; }

    /// <summary>Gets or sets a value indicating whether the Discord webhook post succeeded.</summary>
    public bool DiscordPosted { get; set; }
}
