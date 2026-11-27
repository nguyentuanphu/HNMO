// <copyright file="ResetCoinCost.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ResetOptions;

using System.Globalization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// Coins a reset costs.
/// </summary>
/// <param name="CoinType">The coin type.</param>
/// <param name="Amount">The amount.</param>
public sealed record ResetCoinCost(CashShopCoinType CoinType, int Amount);
