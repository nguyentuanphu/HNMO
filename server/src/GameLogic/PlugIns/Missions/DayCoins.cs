// <copyright file="DayCoins.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Missions;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;

/// <summary>
/// The coins of a day of the login calendar.
/// </summary>
public class DayCoins
{
    /// <summary>Gets or sets the day number (the n-th login day of the month).</summary>
    [Range(1, 31)]
    public int Day { get; set; }

    /// <summary>Gets or sets the coins.</summary>
    [Range(0, 1_000_000)]
    public int Coins { get; set; }
}
