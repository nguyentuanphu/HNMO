// <copyright file="EscrowCurrency.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
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
}
