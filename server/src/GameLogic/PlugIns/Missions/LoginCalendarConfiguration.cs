// <copyright file="LoginCalendarConfiguration.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Missions;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;

/// <summary>
/// The monthly login calendar: one claim per day.
/// </summary>
public class LoginCalendarConfiguration
{
    /// <summary>Gets or sets a value indicating whether the source gives coins.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the coin type.</summary>
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets or sets the coins of a day which isn't in <see cref="Days"/>.</summary>
    [Range(0, 1_000_000)]
    public int DefaultCoins { get; set; } = 5;

    /// <summary>Gets or sets the days with other coins.</summary>
    public ICollection<DayCoins> Days { get; set; } = new List<DayCoins>
    {
        new() { Day = 7, Coins = 20 },
        new() { Day = 14, Coins = 30 },
        new() { Day = 28, Coins = 60 },
    };

    /// <summary>
    /// Gets the coins of a day number.
    /// </summary>
    /// <param name="day">The day number.</param>
    /// <returns>The coins.</returns>
    public int CoinsOf(int day) => this.Days.LastOrDefault(d => d.Day == day)?.Coins ?? this.DefaultCoins;
}
