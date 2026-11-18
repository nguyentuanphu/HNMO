// <copyright file="EscrowWebhookNonceEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
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
