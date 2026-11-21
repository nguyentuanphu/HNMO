// <copyright file="VipBenefits.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

using HNMO.DataModel.Configuration;

/// <summary>Active benefit values for a future gameplay adapter; querying them applies nothing.</summary>
/// <param name="ExperiencePercent">Additional experience percentage.</param>
/// <param name="ZenPercent">Additional zen amount percentage.</param>
/// <param name="DailyCoins">Daily bonus amount.</param>
/// <param name="DailyCoinType">The daily bonus currency.</param>
public sealed record VipBenefits(int ExperiencePercent, int ZenPercent, int DailyCoins, CashShopCoinType DailyCoinType);
