// <copyright file="EscrowWebhookNonceEntry.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Escrow;

using System;

/// <summary>
/// Recorded webhook nonce entry used to prevent replay attacks on per-seller payment webhooks.
/// </summary>
public sealed class EscrowWebhookNonceEntry
{
    /// <summary>Gets or sets target contract identifier.</summary>
    public string ContractId { get; set; } = string.Empty;

    /// <summary>Gets or sets signed request nonce.</summary>
    public string Nonce { get; set; } = string.Empty;

    /// <summary>Gets or sets signed unix timestamp (seconds).</summary>
    public long TimestampUnix { get; set; }

    /// <summary>Gets or sets UTC time recorded.</summary>
    public DateTime RecordedAtUtc { get; set; }
}
