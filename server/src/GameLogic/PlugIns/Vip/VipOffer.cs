// <copyright file="VipOffer.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

using HNMO.DataModel.Configuration;

/// <summary>A host-configured price for one VIP duration.</summary>
/// <param name="Days">The purchased number of days.</param>
/// <param name="Price">The nonnegative coin price.</param>
/// <param name="CoinType">The currency to charge.</param>
public sealed record VipOffer(int Days, int Price, CashShopCoinType CoinType);
