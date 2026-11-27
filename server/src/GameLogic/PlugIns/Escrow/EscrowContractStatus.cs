// <copyright file="EscrowContractStatus.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Escrow;

/// <summary>
/// Lifecycle status of an escrow contract.
/// </summary>
public enum EscrowContractStatus
{
    /// <summary>Open and awaiting buyer payment/acceptance.</summary>
    Open = 0,

    /// <summary>Atomically swapped and delivered through owed rewards.</summary>
    Completed = 1,

    /// <summary>Cancelled prior to payment; escrowed goods returned.</summary>
    Cancelled = 2,

    /// <summary>Expired past deadline; escrowed goods returned.</summary>
    Expired = 3,

    /// <summary>Frozen by admin review.</summary>
    Frozen = 4,

    /// <summary>Direct/host VND contract reserved by a buyer for bank transfer.</summary>
    ReservedForPayment = 5,

    /// <summary>Buyer marked transfer sent and awaits seller/host confirmation.</summary>
    BuyerMarkedPaid = 6,

    /// <summary>Disputed transaction awaiting admin evidence review.</summary>
    Disputed = 7,
}
