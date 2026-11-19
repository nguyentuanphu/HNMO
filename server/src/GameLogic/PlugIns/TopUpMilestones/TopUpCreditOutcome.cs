// <copyright file="TopUpCreditOutcome.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TopUpMilestones;

using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// Outcome of recording a top-up VND credit for an account.
/// </summary>
/// <param name="Recorded">False when the reference was already counted or amount was non-positive.</param>
/// <param name="NewTotalVnd">The account's cumulative VND total.</param>
/// <param name="NewlyUnlockedSteps">Milestone steps newly crossed by this top-up.</param>
public sealed record TopUpCreditOutcome(
    bool Recorded,
    long NewTotalVnd,
    IReadOnlyList<TopUpMilestoneStep> NewlyUnlockedSteps);
