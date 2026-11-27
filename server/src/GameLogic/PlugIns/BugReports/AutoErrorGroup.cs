// <copyright file="AutoErrorGroup.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.BugReports;

/// <summary>The automatic error reports of one fingerprint (message + file + line).</summary>
public sealed class AutoErrorGroup
{
    /// <summary>Gets or sets the group's id (e.g. <c>E4</c>).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the fingerprint the client computed.</summary>
    public string Fingerprint { get; set; } = string.Empty;

    /// <summary>Gets or sets the kind of the first report.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Gets or sets the message of the first report.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Gets or sets how many reports came.</summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets the accounts which sent it, for counting them only (never shown to the developer, never sent anywhere).
    /// </summary>
    public List<string> Accounts { get; set; } = [];

    /// <summary>Gets or sets when the first report came (UTC).</summary>
    public DateTime FirstSeenUtc { get; set; }

    /// <summary>Gets or sets when the last report came (UTC).</summary>
    public DateTime LastSeenUtc { get; set; }

    /// <summary>Gets or sets the client versions it came from (at most 10, newest last).</summary>
    public List<string> Versions { get; set; } = [];

    /// <summary>Gets or sets the last samples (at most 5, newest last).</summary>
    public List<AutoErrorSample> Samples { get; set; } = [];

    /// <summary>Gets or sets the owner's decision.</summary>
    public AutoErrorStatus Status { get; set; }

    /// <summary>Gets or sets a value indicating whether it came back after it was marked fixed.</summary>
    public bool Reopened { get; set; }

    /// <summary>Gets or sets when the owner chose to send it to the HNMO developer (UTC); it goes with the next weekly report.</summary>
    public DateTime? QueuedUtc { get; set; }

    /// <summary>Gets or sets when it was sent to the HNMO developer (UTC).</summary>
    public DateTime? ForwardedUtc { get; set; }
}
