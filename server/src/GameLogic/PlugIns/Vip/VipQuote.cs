// <copyright file="VipQuote.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

/// <summary>The result of quoting a VIP purchase.</summary>
/// <param name="Result">The rule outcome.</param>
/// <param name="Plan">The valid quote, when available.</param>
public sealed record VipQuote(VipRuleResult Result, VipPurchasePlan? Plan = null);
