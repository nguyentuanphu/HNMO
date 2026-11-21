// <copyright file="CatchupCapSnapshot.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.CatchupCap;

using System.Globalization;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// Computed Catch-up Level/Reset Cap snapshot for a specific timestamp.
/// </summary>
/// <param name="LevelCapEnabled">Whether level cap mode is active.</param>
/// <param name="TodayLevelCap">Current level cap for all players.</param>
/// <param name="InitialLevelCap">Initial cap / trigger level.</param>
/// <param name="HardMaxLevelCap">Absolute ceiling level.</param>
/// <param name="LevelTriggered">Whether the first player has reached the initial cap.</param>
/// <param name="LevelFirstPlayer">Name of first player who reached initial cap.</param>
/// <param name="ResetCapEnabled">Whether reset daily cap mode is active.</param>
/// <param name="TodayDailyResetCap">Current daily reset cap.</param>
/// <param name="ResetTriggered">Whether the reset growth trigger has fired.</param>
/// <param name="ResetFirstPlayer">Name of first player who reached trigger resets.</param>
/// <param name="NextDayStartsAtUtc">UTC timestamp of next Sydney day tick when caps increment.</param>
public sealed record CatchupCapSnapshot(
    bool LevelCapEnabled,
    int TodayLevelCap,
    int InitialLevelCap,
    int HardMaxLevelCap,
    bool LevelTriggered,
    string LevelFirstPlayer,
    bool ResetCapEnabled,
    int TodayDailyResetCap,
    bool ResetTriggered,
    string ResetFirstPlayer,
    DateTime NextDayStartsAtUtc);
