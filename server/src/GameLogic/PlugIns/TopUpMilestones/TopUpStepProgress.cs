// <copyright file="TopUpStepProgress.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TopUpMilestones;

using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// View of a single milestone step for a specific account.
/// </summary>
/// <param name="Step">The step configuration.</param>
/// <param name="Reached">Whether the account has reached the threshold.</param>
/// <param name="Claimed">Whether the account has already claimed the step.</param>
/// <param name="Claimable">Whether the account can claim the step right now.</param>
public sealed record TopUpStepProgress(
    TopUpMilestoneStep Step,
    bool Reached,
    bool Claimed,
    bool Claimable);
