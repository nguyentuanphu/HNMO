// <copyright file="ActivityLogQuery.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ActivityLog;

/// <summary>
/// Filter parameters for querying the HNMO player activity log.
/// </summary>
public sealed class ActivityLogQuery
{
    /// <summary>Gets or sets optional account substring filter (matches Account or PartnerAccount).</summary>
    public string? Account { get; set; }

    /// <summary>Gets or sets optional character substring filter (matches Character or PartnerCharacter).</summary>
    public string? Character { get; set; }

    /// <summary>Gets or sets optional item name, GUID, or detail substring filter.</summary>
    public string? Item { get; set; }

    /// <summary>Gets or sets a filter for the exact origin serial, excluding old entity IDs and unstructured text.</summary>
    public Guid? ItemSerial { get; set; }

    /// <summary>Gets or sets optional activity type filter.</summary>
    public ActivityKind? Kind { get; set; }

    /// <summary>Gets or sets an exact combat mode filter.</summary>
    public ActivityPvpMode? PvpMode { get; set; }

    /// <summary>Gets or sets optional inclusive lower bound (UTC).</summary>
    public DateTime? FromUtc { get; set; }

    /// <summary>Gets or sets optional inclusive upper bound (UTC).</summary>
    public DateTime? ToUtc { get; set; }

    /// <summary>Gets or sets maximum entries to return (1..2000).</summary>
    public int MaxResults { get; set; } = 200;
}
