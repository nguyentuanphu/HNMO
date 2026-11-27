// <copyright file="EscrowCurrency.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Escrow;

/// <summary>
/// Supported escrow contract pricing and jewel-escrow currencies.
/// </summary>
public enum EscrowCurrency
{
    /// <summary>In-game Zen.</summary>
    Zen = 0,

    /// <summary>Account WCoin (C).</summary>
    WCoinC = 1,

    /// <summary>Account WCoin (P).</summary>
    WCoinP = 2,

    /// <summary>Account Goblin Points.</summary>
    GoblinPoints = 3,

    /// <summary>Jewel of Bless (via Jewel Bank).</summary>
    JewelBless = 4,

    /// <summary>Jewel of Soul (via Jewel Bank).</summary>
    JewelSoul = 5,

    /// <summary>Jewel of Chaos (via Jewel Bank).</summary>
    JewelChaos = 6,

    /// <summary>Jewel of Creation (via Jewel Bank).</summary>
    JewelCreation = 7,

    /// <summary>Jewel of Life (via Jewel Bank).</summary>
    JewelLife = 8,

    /// <summary>Separate convertible Trade Credit currency (ROADMAP 16.6, OFF by default).</summary>
    TradeCredit = 9,
}
