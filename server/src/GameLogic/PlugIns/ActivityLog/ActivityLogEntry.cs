// <copyright file="ActivityLogEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ActivityLog;

/// <summary>
/// Single persisted player activity record stored individually in <c>hnmo.player_feature_state</c>.
/// </summary>
public sealed class ActivityLogEntry
{
    /// <summary>Gets or sets unique row key (sortable UTC timestamp + unique suffix).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets event UTC timestamp.</summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>Gets or sets activity type.</summary>
    public ActivityKind Kind { get; set; }

    /// <summary>Gets or sets primary account login.</summary>
    public string Account { get; set; } = string.Empty;

    /// <summary>Gets or sets primary character name.</summary>
    public string Character { get; set; } = string.Empty;

    /// <summary>Gets or sets partner account login (trades / personal store / market).</summary>
    public string PartnerAccount { get; set; } = string.Empty;

    /// <summary>Gets or sets partner character or NPC name.</summary>
    public string PartnerCharacter { get; set; } = string.Empty;

    /// <summary>Gets or sets human-readable summary of involved items.</summary>
    public string ItemSummary { get; set; } = string.Empty;

    /// <summary>Gets or sets legacy entity identifiers for older rows.</summary>
    public string ItemIds { get; set; } = string.Empty;

    /// <summary>Gets or sets comma-separated origin serials, assigned to item instances.</summary>
    public string ItemSerials { get; set; } = string.Empty;

    /// <summary>Gets or sets snapshots before the action.</summary>
    public ActivityItemState[]? BeforeItems { get; set; }

    /// <summary>Gets or sets snapshots after the action.</summary>
    public ActivityItemState[]? AfterItems { get; set; }

    /// <summary>Gets or sets the sub-server where the event happened.</summary>
    public byte? ServerId { get; set; }

    /// <summary>Gets or sets the map number when known.</summary>
    public short? MapNumber { get; set; }

    /// <summary>Gets or sets the rules in effect when a player kill occurred.</summary>
    public ActivityPvpMode? PvpMode { get; set; }

    /// <summary>Gets or sets the item units transferred by a pickup, including a partial stack.</summary>
    public double? PickupQuantity { get; set; }

    /// <summary>Gets or sets zen delta associated with the action.</summary>
    public long ZenDelta { get; set; }

    /// <summary>Gets or sets extra structured context (map name, mix result, market ID, etc.).</summary>
    public string Details { get; set; } = string.Empty;
}
