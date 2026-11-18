// <copyright file="EscrowFeeLedgerEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Escrow;

using System;

/// <summary>
/// Recorded host fee collected from an escrow contract for Economy Dashboard reporting.
/// </summary>
public sealed class EscrowFeeLedgerEntry
{
    /// <summary>Gets or sets UTC timestamp.</summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>Gets or sets Sydney local timestamp string.</summary>
    public string TimestampSydney { get; set; } = string.Empty;

    /// <summary>Gets or sets contract id.</summary>
    public string ContractId { get; set; } = string.Empty;

    /// <summary>Gets or sets escrow mode (<c>InGame</c>, <c>DirectVnd</c>, <c>HostVnd</c>, <c>TradeCredit</c>).</summary>
    public string Mode { get; set; } = "InGame";

    /// <summary>Gets or sets fee currency string.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Gets or sets fee amount collected.</summary>
    public long FeeAmount { get; set; }
}
