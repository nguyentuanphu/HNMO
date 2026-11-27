// <copyright file="TradeCreditLedgerEntry.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Escrow;

using System;

/// <summary>
/// Double-entry audit ledger row for Trade Credit top-ups, cash-sale spend/proceeds, conversions, cash-outs, and fees.
/// </summary>
public sealed class TradeCreditLedgerEntry
{
    /// <summary>Gets or sets unique ledger row id.</summary>
    public string EntryId { get; set; } = Guid.NewGuid().ToString("N")[..12];

    /// <summary>Gets or sets UTC timestamp.</summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>Gets or sets Sydney local display timestamp.</summary>
    public string TimestampSydney { get; set; } = string.Empty;

    /// <summary>Gets or sets affected account login (or HOST).</summary>
    public string AccountLogin { get; set; } = string.Empty;

    /// <summary>Gets or sets operation kind (<c>TopUp</c>, <c>CashSaleSpend</c>, <c>CashSaleProceeds</c>, <c>ConvertToCoins</c>, <c>CashOutRequest</c>, <c>CashOutPaid</c>, <c>Fee</c>).</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Gets or sets signed credit units delta.</summary>
    public long Units { get; set; }

    /// <summary>Gets or sets VND equivalent at fixed rate.</summary>
    public long VndAmount { get; set; }

    /// <summary>Gets or sets external or contract reference.</summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>Gets or sets human-readable note.</summary>
    public string Note { get; set; } = string.Empty;
}
