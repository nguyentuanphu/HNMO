// <copyright file="WorldBossKillOutcome.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
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
