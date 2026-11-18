// <copyright file="EscrowDirectVndDetails.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Escrow;

using System;

/// <summary>
/// Direct P2P real-money (VND) payment metadata for a <c>DirectVnd</c> escrow contract.
/// Seller bank details remain hidden from non-participants and from public browse until accepted.
/// </summary>
public sealed class EscrowDirectVndDetails
{
    /// <summary>Gets or sets seller 6-digit NAPAS bank BIN.</summary>
    public string BankBin { get; set; } = string.Empty;

    /// <summary>Gets or sets seller bank short name or label.</summary>
    public string BankName { get; set; } = string.Empty;

    /// <summary>Gets or sets seller bank account number.</summary>
    public string BankAccountNumber { get; set; } = string.Empty;

    /// <summary>Gets or sets seller bank account holder name.</summary>
    public string BankAccountName { get; set; } = string.Empty;

    /// <summary>Gets or sets unique payment memo reference (<c>HNMO-XXXXXX</c>).</summary>
    public string PaymentReference { get; set; } = string.Empty;

    /// <summary>Gets or sets optional per-seller HMAC-SHA256 webhook secret.</summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether an automatic HMAC webhook confirmed payment.</summary>
    public bool WebhookVerified { get; set; }

    /// <summary>Gets or sets optional webhook verification note or error trail.</summary>
    public string WebhookNote { get; set; } = string.Empty;

    /// <summary>Gets or sets coin currency used for optional seller security bond.</summary>
    public EscrowCurrency SellerBondCurrency { get; set; } = EscrowCurrency.WCoinC;

    /// <summary>Gets or sets optional seller coin bond held until completion.</summary>
    public long SellerBondCoins { get; set; }

    /// <summary>Gets or sets a value indicating whether the seller bond was refunded.</summary>
    public bool BondReleased { get; set; }

    /// <summary>Gets or sets a value indicating whether the seller bond was forfeited in dispute.</summary>
    public bool BondForfeited { get; set; }

    /// <summary>Gets or sets service fee coin currency collected on direct VND trade.</summary>
    public EscrowCurrency ServiceFeeCurrency { get; set; } = EscrowCurrency.WCoinC;

    /// <summary>Gets or sets total service fee charged in coins.</summary>
    public long ServiceFeeCoins { get; set; }

    /// <summary>Gets or sets UTC timestamp when buyer reserved contract for payment.</summary>
    public DateTime? ReservedAtUtc { get; set; }

    /// <summary>Gets or sets UTC deadline for buyer bank transfer.</summary>
    public DateTime? PaymentDeadlineUtc { get; set; }

    /// <summary>Gets or sets Sydney local payment deadline display string.</summary>
    public string PaymentDeadlineSydney { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the buyer used their one-time window extension.</summary>
    public bool ExtendedOnce { get; set; }

    /// <summary>Gets or sets UTC timestamp when buyer clicked "I've paid".</summary>
    public DateTime? BuyerMarkedPaidAtUtc { get; set; }

    /// <summary>Gets or sets optional buyer payment note.</summary>
    public string BuyerNote { get; set; } = string.Empty;

    /// <summary>Gets or sets optional verified receipt image MIME type (<c>image/png</c>, <c>image/jpeg</c>, <c>image/webp</c>).</summary>
    public string ReceiptMime { get; set; } = string.Empty;

    /// <summary>Gets or sets optional base64-encoded receipt image payload.</summary>
    public string ReceiptBase64 { get; set; } = string.Empty;

    /// <summary>Gets or sets UTC timestamp when dispute was opened.</summary>
    public DateTime? DisputeOpenedAtUtc { get; set; }

    /// <summary>Gets or sets account login that raised the dispute (or <c>SYSTEM</c>).</summary>
    public string DisputeOpenedBy { get; set; } = string.Empty;

    /// <summary>Gets or sets dispute reason note.</summary>
    public string DisputeReason { get; set; } = string.Empty;

    /// <summary>Gets or sets final admin dispute resolution summary.</summary>
    public string DisputeResolution { get; set; } = string.Empty;
}
