// <copyright file="TradeCreditAccountState.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Escrow;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Per-account Trade Credit balances separating non-cashoutable top-up units from cash-sale proceeds
/// and verified one-time bank binding.
/// </summary>
public sealed class TradeCreditAccountState
{
    /// <summary>Gets or sets account login name.</summary>
    public string LoginName { get; set; } = string.Empty;

    /// <summary>Gets or sets non-cashoutable Trade Credit units from top-ups (spendable only or 1-way coin conversion).</summary>
    public long GeneralUnits { get; set; }

    /// <summary>Gets or sets units currently reserved in pending cash-out requests.</summary>
    public long LockedCashOutUnits { get; set; }

    /// <summary>Gets or sets earned cash-sale proceed lots subject to holding period.</summary>
    public List<TradeCreditProceedLot> ProceedLots { get; set; } = [];

    /// <summary>Gets or sets verified NAPAS bank BIN entered once for cash-outs.</summary>
    public string VerifiedBankBin { get; set; } = string.Empty;

    /// <summary>Gets or sets verified bank name.</summary>
    public string VerifiedBankName { get; set; } = string.Empty;

    /// <summary>Gets or sets verified bank account number.</summary>
    public string VerifiedBankAccountNumber { get; set; } = string.Empty;

    /// <summary>Gets or sets verified bank holder name.</summary>
    public string VerifiedBankAccountName { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether seller payout bank is locked against arbitrary re-entry.</summary>
    public bool BankLocked { get; set; }

    /// <summary>Gets or sets UTC timestamp when bank was verified.</summary>
    public DateTime? BankVerifiedAtUtc { get; set; }

    /// <summary>Gets total spendable balance across top-up units and sale proceeds.</summary>
    public long SpendableUnits => Math.Max(0, this.GeneralUnits) + this.ProceedLots.Sum(l => Math.Max(0, l.RemainingUnits));

    /// <summary>Computes cash-out eligible proceeds at <paramref name="utcNow"/>.</summary>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>Cash-out eligible units.</returns>
    public long EligibleCashOutUnits(DateTime utcNow) =>
        this.ProceedLots.Where(l => l.RemainingUnits > 0 && utcNow >= l.EligibleAtUtc).Sum(l => l.RemainingUnits);
}
