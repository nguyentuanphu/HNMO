// <copyright file="VipWalletResult.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

/// <summary>A machine-readable wallet outcome; callers localize the status and rule refusal.</summary>
/// <param name="Status">Applied, Pending, Refused, Conflict, or PriceChanged.</param>
/// <param name="Rule">The underlying quote refusal, if any.</param>
/// <param name="Membership">The applied interval, if known.</param>
public sealed record VipWalletResult(string Status, VipRuleResult? Rule = null, VipMembership? Membership = null);
