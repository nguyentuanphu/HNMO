// <copyright file="VipQuote.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

/// <summary>The result of quoting a VIP purchase.</summary>
/// <param name="Result">The rule outcome.</param>
/// <param name="Plan">The valid quote, when available.</param>
public sealed record VipQuote(VipRuleResult Result, VipPurchasePlan? Plan = null);
