// <copyright file="ResetCoinCost.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
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
