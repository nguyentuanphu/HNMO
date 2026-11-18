// <copyright file="VipWalletResult.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

/// <summary>A machine-readable wallet outcome; callers localize the status and rule refusal.</summary>
/// <param name="Status">Applied, Pending, Refused, Conflict, or PriceChanged.</param>
/// <param name="Rule">The underlying quote refusal, if any.</param>
/// <param name="Membership">The applied interval, if known.</param>
public sealed record VipWalletResult(string Status, VipRuleResult? Rule = null, VipMembership? Membership = null);
