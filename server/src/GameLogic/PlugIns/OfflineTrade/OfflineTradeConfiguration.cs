// <copyright file="OfflineTradeConfiguration.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.OfflineTrade;

/// <summary>
/// Configuration for <see cref="OfflineTradePlugIn"/> (disabled until the host activates it).
/// </summary>
public sealed class OfflineTradeConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether players must stand inside a safe zone to start <c>/offtrade</c>.
    /// </summary>
    public bool SafeZoneOnly { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether personal shops priced in account coins or jewels (<c>WCoinC</c>, <c>WCoinP</c>, <c>GoblinPoints</c>, etc.) are allowed in offline trade.
    /// </summary>
    public bool AllowCoinCurrencies { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum hours an offline shop remains active before logging off automatically (<c>0</c> = unlimited).
    /// </summary>
    public int MaxDurationHours { get; set; } = 48;

    /// <summary>
    /// Gets or sets an optional Zen fee charged when starting <c>/offtrade</c> (<c>0</c> = free).
    /// </summary>
    public int OpenFeeZen { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the offline shop automatically saves and logs off once the last item is sold.
    /// </summary>
    public bool StopWhenSoldOut { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum character level required to start <c>/offtrade</c>.
    /// </summary>
    public int MinimumLevel { get; set; } = 6;

    /// <summary>
    /// Gets or sets optional map numbers where <c>/offtrade</c> is allowed. Empty allows all maps.
    /// </summary>
    public List<short> AllowedMapNumbers { get; set; } = [];
}
