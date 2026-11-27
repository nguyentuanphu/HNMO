// <copyright file="WorldBossKillOutcome.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.WorldBosses;

using System.Globalization;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// The result of a kill: the record for the website and the admin panel, and the rewards which were earned.
/// </summary>
/// <param name="Record">The record.</param>
/// <param name="Rewards">The rewards by character id.</param>
public sealed record WorldBossKillOutcome(WorldBossKillRecord Record, IReadOnlyDictionary<string, List<WorldBossOwedReward>> Rewards);
