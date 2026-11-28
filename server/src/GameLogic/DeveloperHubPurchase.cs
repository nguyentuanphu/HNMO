// <copyright file="DeveloperHubPurchase.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic;

/// <summary>A purchase or trial whose licence the server waits for (it polls the hub every 30 seconds for 30 minutes).</summary>
public sealed class DeveloperHubPurchase
{
    /// <summary>Gets or sets the package bought.</summary>
    public string PackageId { get; set; } = string.Empty;

    /// <summary>Gets or sets the hub's order id; empty for a trial.</summary>
    public string Order { get; set; } = string.Empty;

    /// <summary>Gets or sets how it is paid: stripe, payos or trial.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Gets or sets when it began (UTC).</summary>
    public DateTime StartedUtc { get; set; }

    /// <summary>Gets or sets the id of the licence in use when it began: a licence with another id is the new one.</summary>
    public string? PreviousLicenceId { get; set; }

    /// <summary>Gets or sets how many times the hub was asked by the schedule.</summary>
    public int Attempts { get; set; }

    /// <summary>Gets or sets when the hub is asked next (UTC).</summary>
    public DateTime NextCheckUtc { get; set; }

    /// <summary>Gets or sets when the hub was last asked (UTC), by the schedule, the page or the host's button.</summary>
    public DateTime? LastCheckUtc { get; set; }

    /// <summary>Gets or sets the bank transfer to make (provider bank), shown again on the License page.</summary>
    public HubBankTransfer? Bank { get; set; }
}
