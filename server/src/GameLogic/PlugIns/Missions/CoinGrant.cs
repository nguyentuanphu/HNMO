// <copyright file="CoinGrant.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Missions;

using System.Security.Cryptography;
using HNMO.DataModel.Configuration;

/// <summary>
/// A coin grant which the engine decided on; the plug-in gives it.
/// </summary>
/// <param name="LoginName">The account.</param>
/// <param name="CoinType">The coin type.</param>
/// <param name="Amount">The amount.</param>
/// <param name="Reason">The reason, for the coins history.</param>
/// <param name="Reference">The unique reference, so it counts once.</param>
public sealed record CoinGrant(string LoginName, CashShopCoinType CoinType, int Amount, string Reason, string Reference);
