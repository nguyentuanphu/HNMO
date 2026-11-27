// <copyright file="TradeCreditProceedLot.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Escrow;

using System;

/// <summary>
/// Tracks an individual Trade Credit lot earned specifically from a completed cash-sale escrow contract,
/// enforcing the holding period before cash-out eligibility.
/// </summary>
public sealed class TradeCreditProceedLot
{
    /// <summary>Gets or sets unique lot identifier.</summary>
    public string LotId { get; set; } = Guid.NewGuid().ToString("N")[..12];

    /// <summary>Gets or sets originating contract identifier.</summary>
    public string ContractId { get; set; } = string.Empty;

    /// <summary>Gets or sets original earned net units.</summary>
    public long OriginalUnits { get; set; }

    /// <summary>Gets or sets remaining unspent / uncashed units in this lot.</summary>
    public long RemainingUnits { get; set; }

    /// <summary>Gets or sets UTC timestamp when sale proceeds were earned.</summary>
    public DateTime EarnedAtUtc { get; set; }

    /// <summary>Gets or sets UTC timestamp after which remaining units become cash-out eligible.</summary>
    public DateTime EligibleAtUtc { get; set; }
}
