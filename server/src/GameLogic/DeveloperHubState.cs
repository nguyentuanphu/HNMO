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

    /// <summary>Gets or sets when the developer hub was last reached (UTC).</summary>
    public DateTime? LastHubReachUtc { get; set; }

    /// <summary>Gets or sets when hub unreachability began continuously (UTC); cleared on any response.</summary>
    public DateTime? FirstHubUnreachableUtc { get; set; }

    /// <summary>Gets or sets the key the hub gave this computer for forwarding automatic error reports.</summary>
    public string? AutoErrorKey { get; set; }

    /// <summary>Gets or sets the purchase or trial whose licence is awaited; none: nothing is awaited.</summary>
    public DeveloperHubPurchase? Purchase { get; set; }

    /// <summary>Gets or sets whether the hub said this computer already had its free trial.</summary>
    public bool TrialUsed { get; set; }

    /// <summary>Gets or sets the hub's last licence package list (JSON), kept for <see cref="DeveloperHub.PackageCacheAge"/>.</summary>
    public string? PackagesJson { get; set; }

    /// <summary>Gets or sets when <see cref="PackagesJson"/> was fetched (UTC).</summary>
    public DateTime? PackagesFetchedUtc { get; set; }

    /// <summary>
    /// Gets or sets the hub's switch for the licence's folder limit, from its last answer (kept under
    /// <see cref="HNMO.Interfaces.HubFolderLimits.StatePropertyName"/>, which the launcher writes too); none: off.
    /// </summary>
    public HNMO.Interfaces.HubFolderLimits? FolderLimits { get; set; }
}
