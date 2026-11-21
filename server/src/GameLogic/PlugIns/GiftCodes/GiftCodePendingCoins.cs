// <copyright file="GiftCodePendingCoins.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GiftCodes;

using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.Persistence;

/// <summary>
/// Coins of a redemption which weren't given yet (they're given once, by their reference).
/// </summary>
public sealed class GiftCodePendingCoins
{
    /// <summary>Gets or sets the reference.</summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>Gets or sets the account's login name.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Gets or sets the coin type.</summary>
    public CashShopCoinType CoinType { get; set; }

    /// <summary>Gets or sets the amount.</summary>
    public int Amount { get; set; }

    /// <summary>Gets or sets the reason in the coins history.</summary>
    public string Reason { get; set; } = string.Empty;
}
