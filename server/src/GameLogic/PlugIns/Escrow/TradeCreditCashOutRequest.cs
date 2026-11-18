// <copyright file="TradeCreditCashOutRequest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Escrow;

using System;

/// <summary>
/// Queued Trade Credit cash-out payout request backed strictly by matured cash-sale proceeds.
/// </summary>
public sealed class TradeCreditCashOutRequest
{
    /// <summary>Gets or sets request identifier.</summary>
    public string RequestId { get; set; } = "TC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    /// <summary>Gets or sets UTC request creation time.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Gets or sets Sydney local creation time.</summary>
    public string CreatedAtSydney { get; set; } = string.Empty;

    /// <summary>Gets or sets seller login name.</summary>
    public string AccountLogin { get; set; } = string.Empty;

    /// <summary>Gets or sets verified NAPAS bank BIN snapshot.</summary>
    public string BankBin { get; set; } = string.Empty;

    /// <summary>Gets or sets verified bank name snapshot.</summary>
    public string BankName { get; set; } = string.Empty;

    /// <summary>Gets or sets verified bank account number snapshot.</summary>
    public string BankAccountNumber { get; set; } = string.Empty;

    /// <summary>Gets or sets verified bank account holder snapshot.</summary>
    public string BankAccountName { get; set; } = string.Empty;

    /// <summary>Gets or sets gross Trade Credit units redeemed.</summary>
    public long GrossUnits { get; set; }

    /// <summary>Gets or sets host cash-out fee in units.</summary>
    public long FeeUnits { get; set; }

    /// <summary>Gets or sets net units cashed out after fee.</summary>
    public long NetUnits { get; set; }

    /// <summary>Gets or sets net VND payout amount.</summary>
    public long NetVnd { get; set; }

    /// <summary>Gets or sets status (<c>Queued</c>, <c>Paid</c>).</summary>
    public string Status { get; set; } = "Queued";

    /// <summary>Gets or sets UTC time marked paid by admin.</summary>
    public DateTime? PaidAtUtc { get; set; }

    /// <summary>Gets or sets admin payout bank reference.</summary>
    public string PayoutReference { get; set; } = string.Empty;
}
