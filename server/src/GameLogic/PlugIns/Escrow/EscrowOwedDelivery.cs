// <copyright file="EscrowOwedDelivery.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Escrow;

using System;
using System.Collections.Generic;

/// <summary>
/// Atomic owed-reward delivery created on contract swap, cancellation, or expiry so goods/Zen/Jewels are never lost offline or on full bag.
/// </summary>
public sealed class EscrowOwedDelivery
{
    /// <summary>Gets or sets unique delivery identifier.</summary>
    public string DeliveryId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets or sets related contract id.</summary>
    public string ContractId { get; set; } = string.Empty;

    /// <summary>Gets or sets recipient account login name.</summary>
    public string RecipientLogin { get; set; } = string.Empty;

    /// <summary>Gets or sets English summary reason.</summary>
    public string ReasonEn { get; set; } = string.Empty;

    /// <summary>Gets or sets Vietnamese summary reason.</summary>
    public string ReasonVi { get; set; } = string.Empty;

    /// <summary>Gets or sets items to deliver into player inventory.</summary>
    public List<EscrowItemSnapshot> Items { get; set; } = [];

    /// <summary>Gets or sets Zen to deliver.</summary>
    public long Zen { get; set; }

    /// <summary>Gets or sets Jewel Bank jewels to credit immediately.</summary>
    public List<EscrowJewelLine> Jewels { get; set; } = [];

    /// <summary>Gets or sets coin currency when refunding bonds or payouts via owed delivery.</summary>
    public EscrowCurrency CoinCurrency { get; set; } = EscrowCurrency.WCoinC;

    /// <summary>Gets or sets coin amount refunded via owed delivery.</summary>
    public long Coins { get; set; }

    /// <summary>Gets or sets UTC creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Gets or sets post-escrow re-trade cooldown end timestamp when items are buyer-received (not seller return).</summary>
    public DateTime? ItemTradeCooldownUntilUtc { get; set; }
}
