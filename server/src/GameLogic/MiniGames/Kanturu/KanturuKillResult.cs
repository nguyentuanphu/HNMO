// <copyright file="KanturuKillResult.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.MiniGames.Kanturu;

/// <summary>
/// The outcome of registering a monster kill.
/// </summary>
/// <param name="Counted">Whether the kill counted towards the current phase.</param>
/// <param name="KillCount">The kill count after registration.</param>
/// <param name="PhaseComplete">Whether the kill target has been reached.</param>
/// <param name="NightmareBossKilled">Whether the Nightmare boss itself died.</param>
/// <param name="IsNightmarePhase">Whether the current phase is a Nightmare phase.</param>
/// <param name="Phase">The phase the kill was counted for, if any.</param>
internal readonly record struct KanturuKillResult(bool Counted, int KillCount, bool PhaseComplete, bool NightmareBossKilled, bool IsNightmarePhase, KanturuPhaseDefinition? Phase);
