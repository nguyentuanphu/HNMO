// <copyright file="VipPurchasePlan.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

using HNMO.DataModel.Configuration;

/// <summary>A quote only: creating this object never debits coins or activates VIP.</summary>
/// <param name="LoginName">The canonical account name.</param>
/// <param name="LevelId">The level to buy.</param>
/// <param name="Days">The offered number of days.</param>
/// <param name="Price">The exact coin price.</param>
/// <param name="CoinType">The required currency.</param>
/// <param name="QuotedAtUtc">The exact time used to calculate the quote.</param>
/// <param name="PreviousUntilUtc">The subscription expiry observed while quoting, or null.</param>
/// <param name="UntilUtc">The expiry after applying the quote.</param>
public sealed record VipPurchasePlan(string LoginName, int LevelId, int Days, int Price, CashShopCoinType CoinType, DateTime QuotedAtUtc, DateTime? PreviousUntilUtc, DateTime UntilUtc);
