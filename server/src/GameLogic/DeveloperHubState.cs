// <copyright file="DeveloperHubState.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic;

/// <summary>What a server keeps of its exchanges with the HNMO developer's hub (<c>save/developer-hub.json</c>).</summary>
public sealed class DeveloperHubState
{
    /// <summary>Gets or sets when the last weekly report was taken (UTC).</summary>
    public DateTime? LastWeeklyUtc { get; set; }

    /// <summary>Gets or sets when a weekly report was last tried (UTC).</summary>
    public DateTime? LastWeeklyTryUtc { get; set; }

    /// <summary>Gets or sets the last licence request.</summary>
    public DeveloperHubRequest? Request { get; set; }

    /// <summary>
    /// Gets or sets when more players than allowed were first in the game at once (UTC): from then, more may play for a
    /// week while the admin panel asks the host to get a licence; after it, the limit holds. A valid licence clears it.
    /// </summary>
    public DateTime? GraceStartedUtc { get; set; }

    /// <summary>Gets or sets when the first-setup report was taken by the hub (UTC); none: not yet.</summary>
    public DateTime? SetupReportedUtc { get; set; }

    /// <summary>Gets or sets when the first-setup report was last tried (UTC).</summary>
    public DateTime? SetupTriedUtc { get; set; }

    /// <summary>Gets or sets the host's name for the reports (from the last licence request).</summary>
    public string HostName { get; set; } = string.Empty;

    /// <summary>Gets or sets how the developer can reach the host (from the last licence request).</summary>
    public string HostContact { get; set; } = string.Empty;

    /// <summary>Gets or sets the ids of licences the developer withdrew.</summary>
    public List<string> WithdrawnLicences { get; set; } = [];
}
