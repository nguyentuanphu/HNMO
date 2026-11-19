// <copyright file="OwedCoinsState.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Missions;

using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.Persistence;

/// <summary>
/// Earned coins which didn't fit under the daily cap; they're given on a later day.
/// </summary>
public sealed class OwedCoinsState
{
    /// <summary>Gets or sets the grant reference.</summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>Gets or sets the reason.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Gets or sets the amount.</summary>
    public int Amount { get; set; }

    /// <summary>Gets or sets the coin type.</summary>
    public CashShopCoinType CoinType { get; set; }
}
