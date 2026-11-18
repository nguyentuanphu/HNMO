// <copyright file="EscrowContract.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Escrow;

using System;
using System.Collections.Generic;

/// <summary>
/// Immutable-keyed escrow contract between seller and named or open buyer.
/// </summary>
public sealed class EscrowContract
{
    /// <summary>Gets or sets contract identifier (<c>C-XXXXXXXX</c>).</summary>
    public string ContractId { get; set; } = string.Empty;

    /// <summary>Gets or sets mode tag (<c>InGame</c>).</summary>
    public string Mode { get; set; } = "InGame";

    /// <summary>Gets or sets current contract status.</summary>
    public EscrowContractStatus Status { get; set; } = EscrowContractStatus.Open;

    /// <summary>Gets or sets UTC creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Gets or sets Sydney local creation string.</summary>
    public string CreatedAtSydney { get; set; } = string.Empty;

    /// <summary>Gets or sets UTC expiration deadline.</summary>
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>Gets or sets Sydney local expiration string.</summary>
    public string ExpiresAtSydney { get; set; } = string.Empty;

    /// <summary>Gets or sets UTC completion or cancellation timestamp.</summary>
    public DateTime? ClosedAtUtc { get; set; }

    /// <summary>Gets or sets seller account login name.</summary>
    public string SellerLogin { get; set; } = string.Empty;

    /// <summary>Gets or sets seller character name.</summary>
    public string SellerCharacter { get; set; } = string.Empty;

    /// <summary>Gets or sets optional named buyer login or character filter (empty = open offer).</summary>
    public string TargetBuyer { get; set; } = string.Empty;

    /// <summary>Gets or sets buyer account login name when accepted.</summary>
    public string AcceptedByLogin { get; set; } = string.Empty;

    /// <summary>Gets or sets buyer character name when accepted.</summary>
    public string AcceptedByCharacter { get; set; } = string.Empty;

    /// <summary>Gets or sets escrowed item snapshots with options and origin summaries.</summary>
    public List<EscrowItemSnapshot> Items { get; set; } = [];

    /// <summary>Gets or sets Zen locked by seller inside escrow.</summary>
    public long EscrowedZen { get; set; }

    /// <summary>Gets or sets Jewel Bank balances locked by seller inside escrow.</summary>
    public List<EscrowJewelLine> EscrowedJewels { get; set; } = [];

    /// <summary>Gets or sets asking price currency.</summary>
    public EscrowCurrency PriceCurrency { get; set; } = EscrowCurrency.WCoinC;

    /// <summary>Gets or sets asking price amount.</summary>
    public long PriceAmount { get; set; }

    /// <summary>Gets or sets fee payer rule snapshotted at contract creation.</summary>
    public EscrowFeePayer FeePayer { get; set; }

    /// <summary>Gets or sets total host fee charged.</summary>
    public long FeeAmount { get; set; }

    /// <summary>Gets or sets buyer total debit including buyer fee share.</summary>
    public long BuyerTotalDebit { get; set; }

    /// <summary>Gets or sets net amount credited to seller after seller fee share.</summary>
    public long NetSellerProceeds { get; set; }

    /// <summary>Gets or sets optional seller note shown to counterparties.</summary>
    public string Note { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether an admin has frozen this contract.</summary>
    public bool FrozenByAdmin { get; set; }

    /// <summary>Gets or sets admin freeze/cancel reason.</summary>
    public string AdminReason { get; set; } = string.Empty;
}
