// <copyright file="TopUpClaimOutcome.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TopUpMilestones;

using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// Outcome of claiming a milestone step.
/// </summary>
/// <param name="Result">The outcome code.</param>
/// <param name="Step">The targeted step, if found.</param>
/// <param name="CoinGrant">The coin grant to apply (with unique reference), if any.</param>
/// <param name="OwedReward">The bag goods (zen/items) to deliver or queue, if any.</param>
public sealed record TopUpClaimOutcome(
    TopUpClaimResult Result,
    TopUpMilestoneStep? Step,
    CoinGrant? CoinGrant,
    GiftCodeOwedReward? OwedReward);
