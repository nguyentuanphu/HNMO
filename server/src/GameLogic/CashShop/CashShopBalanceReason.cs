// <copyright file="CashShopBalanceReason.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.CashShop;

/// <summary>The source of an already-applied direct account balance change.</summary>
public enum CashShopBalanceReason
{
    /// <summary>An extra vault purchase.</summary>
    ExtraVault,

    /// <summary>A purchase from the item market.</summary>
    MarketPurchase,

    /// <summary>A payment to another character's store.</summary>
    PlayerStorePurchase,

    /// <summary>A payment received for a character-store sale.</summary>
    PlayerStoreSale,

    /// <summary>A reset payment.</summary>
    Reset,

    /// <summary>A relife payment.</summary>
    Relife,

    /// <summary>A stat-clearing payment.</summary>
    ClearStats,

    /// <summary>A master-skill-clearing payment.</summary>
    ClearMaster,

    /// <summary>Goblin Points earned for play time.</summary>
    PlayTime,
}
