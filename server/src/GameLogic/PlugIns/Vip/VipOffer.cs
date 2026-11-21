// <copyright file="VipOffer.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

using HNMO.DataModel.Configuration;

/// <summary>A host-configured price for one VIP duration.</summary>
/// <param name="Days">The purchased number of days.</param>
/// <param name="Price">The nonnegative coin price.</param>
/// <param name="CoinType">The currency to charge.</param>
public sealed record VipOffer(int Days, int Price, CashShopCoinType CoinType);
