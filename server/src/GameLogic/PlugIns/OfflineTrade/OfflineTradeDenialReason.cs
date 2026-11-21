// <copyright file="OfflineTradeDenialReason.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.OfflineTrade;

/// <summary>
/// Reasons why a player's request to start <c>/offtrade</c> was refused.
/// </summary>
public enum OfflineTradeDenialReason
{
    /// <summary>No denial; the player may start an offline shop.</summary>
    None = 0,

    /// <summary>The <see cref="OfflineTradePlugIn"/> is disabled.</summary>
    Disabled = 1,

    /// <summary>The player has no selected character or account.</summary>
    NoCharacterSelected = 2,

    /// <summary>The character is dead.</summary>
    NotAlive = 3,

    /// <summary>The character is inside a mini-game.</summary>
    InMiniGame = 4,

    /// <summary>Another offline session (off-level or off-trade) is already active on this account.</summary>
    AlreadyOffline = 5,

    /// <summary>The personal store is not open.</summary>
    StoreNotOpen = 6,

    /// <summary>The personal store contains no items.</summary>
    StoreEmpty = 7,

    /// <summary>The character must stand inside a safe zone.</summary>
    NotInSafeZone = 8,

    /// <summary>The character's current map is not in the configured allowed map list.</summary>
    MapNotAllowed = 9,

    /// <summary>The character level is below <see cref="OfflineTradeConfiguration.MinimumLevel"/>.</summary>
    LevelTooLow = 10,

    /// <summary>Non-Zen prices are disabled by host settings.</summary>
    CoinCurrenciesNotAllowed = 11,

    /// <summary>The player does not have enough Zen for the configured opening fee.</summary>
    NotEnoughZen = 12,
}
