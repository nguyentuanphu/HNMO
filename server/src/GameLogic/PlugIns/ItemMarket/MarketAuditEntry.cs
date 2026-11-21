// <copyright file="MarketAuditEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ItemMarket;

using System.IO;
using System.Text.Json;

/// <summary>
/// Audit log record for every Item Market action.
/// </summary>
public sealed class MarketAuditEntry
{
    /// <summary>Gets or sets UTC timestamp.</summary>
    public DateTime AtUtc { get; set; }

    /// <summary>Gets or sets action (<c>List</c>, <c>Buy</c>, <c>Cancel</c>, <c>Expire</c>, <c>Claim</c>).</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Gets or sets acting account login.</summary>
    public string ActorLogin { get; set; } = string.Empty;

    /// <summary>Gets or sets counterparty login if any.</summary>
    public string CounterpartyLogin { get; set; } = string.Empty;

    /// <summary>Gets or sets listing identifier.</summary>
    public string ListingId { get; set; } = string.Empty;

    /// <summary>Gets or sets item name.</summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>Gets or sets currency name.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Gets or sets gross amount.</summary>
    public int GrossAmount { get; set; }

    /// <summary>Gets or sets deducted fee.</summary>
    public int FeeAmount { get; set; }

    /// <summary>Gets or sets net amount paid to seller.</summary>
    public int NetAmount { get; set; }

    /// <summary>Gets or sets origin (<c>game</c>, <c>website</c>, <c>admin</c>, <c>system</c>).</summary>
    public string Source { get; set; } = string.Empty;
}
