// <copyright file="VipBenefits.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

using HNMO.DataModel.Configuration;

/// <summary>Active benefit values for a future gameplay adapter; querying them applies nothing.</summary>
/// <param name="ExperiencePercent">Additional experience percentage.</param>
/// <param name="ZenPercent">Additional zen amount percentage.</param>
/// <param name="DailyCoins">Daily bonus amount.</param>
/// <param name="DailyCoinType">The daily bonus currency.</param>
public sealed record VipBenefits(int ExperiencePercent, int ZenPercent, int DailyCoins, CashShopCoinType DailyCoinType);
