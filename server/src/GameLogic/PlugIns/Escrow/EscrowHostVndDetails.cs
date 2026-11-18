// <copyright file="EscrowHostVndDetails.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Escrow;

using System;

/// <summary>
/// Host-mediated real-money (VND) settlement metadata (<c>HostVnd</c> mode, ROADMAP 16.2):
/// buyer transfers via Host VietQR with unique <c>HNMO-XXXX</c> reference; seller payout is queued minus host fee
/// for N days while dispute window runs.
/// </summary>
public sealed class EscrowHostVndDetails
{
    /// <summary>Gets or sets seller 6-digit NAPAS bank BIN for host payout.</summary>
    public string SellerBankBin { get; set; } = string.Empty;

    /// <summary>Gets or sets seller bank label for host payout.</summary>
    public string SellerBankName { get; set; } = string.Empty;

    /// <summary>Gets or sets seller bank account number (private to seller &amp; admin payout queue).</summary>
    public string SellerBankAccountNumber { get; set; } = string.Empty;

    /// <summary>Gets or sets seller bank holder name (private to seller &amp; admin payout queue).</summary>
    public string SellerBankAccountName { get; set; } = string.Empty;

    /// <summary>Gets or sets host receiving NAPAS bank BIN snapshot for buyer VietQR.</summary>
    public string HostBankBin { get; set; } = string.Empty;

    /// <summary>Gets or sets host receiving bank name snapshot.</summary>
    public string HostBankName { get; set; } = string.Empty;

    /// <summary>Gets or sets host receiving bank account snapshot.</summary>
    public string HostBankAccountNumber { get; set; } = string.Empty;

    /// <summary>Gets or sets host receiving bank holder name snapshot.</summary>
    public string HostBankAccountName { get; set; } = string.Empty;

    /// <summary>Gets or sets unique payment reference (<c>HNMO-XXXXXXXX</c>).</summary>
    public string PaymentReference { get; set; } = string.Empty;

    /// <summary>Gets or sets UTC buyer reservation timestamp.</summary>
    public DateTime? ReservedAtUtc { get; set; }

    /// <summary>Gets or sets UTC deadline for buyer bank transfer to host.</summary>
    public DateTime? PaymentDeadlineUtc { get; set; }

    /// <summary>Gets or sets Sydney local deadline string.</summary>
    public string PaymentDeadlineSydney { get; set; } = string.Empty;

    /// <summary>Gets or sets confirmed bank transaction id/provider from Casso or SePay webhook.</summary>
    public string PaymentTxId { get; set; } = string.Empty;

    /// <summary>Gets or sets UTC timestamp when host received buyer payment and released item.</summary>
    public DateTime? PaidAtUtc { get; set; }

    /// <summary>Gets or sets UTC dispute window expiry before seller payout unlocks.</summary>
    public DateTime? DisputeWindowEndsUtc { get; set; }

    /// <summary>Gets or sets UTC target payout date for admin payout queue.</summary>
    public DateTime? PayoutDueAtUtc { get; set; }

    /// <summary>Gets or sets Sydney local target payout timestamp.</summary>
    public string PayoutDueAtSydney { get; set; } = string.Empty;

    /// <summary>Gets or sets payout queue status (<c>None</c>, <c>Queued</c>, <c>Disputed</c>, <c>Paid</c>, <c>Refunded</c>).</summary>
    public string PayoutStatus { get; set; } = "None";

    /// <summary>Gets or sets VND fee kept by host.</summary>
    public long HostFeeVnd { get; set; }

    /// <summary>Gets or sets net VND payout owed to seller.</summary>
    public long NetSellerPayoutVnd { get; set; }

    /// <summary>Gets or sets UTC timestamp when admin marked payout completed.</summary>
    public DateTime? PayoutPaidAtUtc { get; set; }

    /// <summary>Gets or sets admin payout bank reference / note.</summary>
    public string PayoutReference { get; set; } = string.Empty;

    /// <summary>Gets or sets optional dispute reason raised inside the dispute window.</summary>
    public string DisputeReason { get; set; } = string.Empty;

    /// <summary>Gets or sets admin dispute resolution summary.</summary>
    public string DisputeResolution { get; set; } = string.Empty;
}
