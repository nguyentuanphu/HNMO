// <copyright file="TopUpClaimOutcome.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
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
