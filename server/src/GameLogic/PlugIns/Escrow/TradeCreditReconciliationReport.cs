// <copyright file="TradeCreditReconciliationReport.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Escrow;

using System;

/// <summary>
/// Daily circulating balance vs ledger reconciliation report (`top-ups - conversions - cash-outs - fees`).
/// </summary>
public sealed class TradeCreditReconciliationReport
{
    /// <summary>Gets or sets UTC check timestamp.</summary>
    public DateTime CheckedAtUtc { get; set; }

    /// <summary>Gets or sets Sydney formatted timestamp.</summary>
    public string CheckedAtSydney { get; set; } = string.Empty;

    /// <summary>Gets or sets total circulating units held across accounts plus pending cash-outs.</summary>
    public long ActualCirculatingUnits { get; set; }

    /// <summary>Gets or sets expected units computed from <c>TopUp - ConvertToCoins - CashOutPaid - Fee</c>.</summary>
    public long ExpectedCirculatingUnits { get; set; }

    /// <summary>Gets or sets cumulative topped-up units.</summary>
    public long TotalTopUpUnits { get; set; }

    /// <summary>Gets or sets cumulative converted-to-coins units.</summary>
    public long TotalConvertedToCoinsUnits { get; set; }

    /// <summary>Gets or sets cumulative paid cash-out units.</summary>
    public long TotalCashOutPaidUnits { get; set; }

    /// <summary>Gets or sets cumulative retained fee units.</summary>
    public long TotalFeeUnits { get; set; }

    /// <summary>Gets or sets locked units awaiting admin cash-out transfer.</summary>
    public long PendingCashOutUnits { get; set; }

    /// <summary>Gets variance (<see cref="ActualCirculatingUnits"/> minus <see cref="ExpectedCirculatingUnits"/>).</summary>
    public long VarianceUnits => this.ActualCirculatingUnits - this.ExpectedCirculatingUnits;

    /// <summary>Gets a value indicating whether books reconcile exactly (<see cref="VarianceUnits"/> == 0).</summary>
    public bool IsBalanced => this.VarianceUnits == 0;
}
