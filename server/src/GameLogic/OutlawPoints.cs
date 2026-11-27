// <copyright file="OutlawPoints.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic;

using HNMO.Interfaces;

/// <summary>
/// Player killer (outlaw) points and stage progression, selected per season via <see cref="HnmoSeasonInstance.IsSeasonNine"/>:
/// <list type="bullet">
/// <item><description><b>Season 6.3 / Arena</b> (<c>PKTIME = 1</c>): each outlaw stage lasts 3 hours (<c>10,800</c> s),
/// stacking <c>10,800</c> -> <c>21,600</c> -> <c>32,400</c> (+<c>10,800</c> per further kill as murderer),
/// decaying <c>1</c>/sec online and <c>floor(MonsterLevel / 2)</c> per killed monster.</description></item>
/// <item><description><b>Season 9</b> (<c>PlayerKillSystem</c> XML thresholds <c>21,600 / 43,200 / 86,400</c>,
/// <c>MurdererPointIncrease = 3,600</c>): 1st kill sets <c>21,600</c> (Warning), 2nd sets <c>64,800</c>
/// (inside PK1 [<c>43,200..86,399</c>]), 3rd sets <c>90,000</c> (inside Murderer &gt;= <c>86,400</c>), further
/// kills add <c>3,600</c>; NO per-second decay, cleared only by monster kills (<c>floor(MonsterLevel / 2)</c>) or <c>/pkclear</c>.</description></item>
/// </list>
/// </summary>
public static class OutlawPoints
{
    /// <summary>
    /// The Season 9 Warning total after the first kill (<see cref="HeroState.PlayerKillWarning"/>).
    /// </summary>
    public const int WarningPoints = 21_600;

    /// <summary>
    /// The Season 9 threshold of <see cref="HeroState.PlayerKiller1stStage"/> (PK level 2).
    /// </summary>
    public const int PlayerKiller1stStagePoints = 43_200;

    /// <summary>
    /// The Season 9 threshold of <see cref="HeroState.PlayerKiller2ndStage"/> (murderer).
    /// </summary>
    public const int PlayerKiller2ndStagePoints = 86_400;

    /// <summary>
    /// The Season 9 entry total on the second kill (<c>43,200 + 21,600 = 64,800</c>), squarely inside PK1 so neither one monster nor one tick exits PK1.
    /// </summary>
    public const int SeasonNineFirstStageEntryPoints = PlayerKiller1stStagePoints + WarningPoints;

    /// <summary>
    /// The Season 9 points which every kill entering or continuing murderer status adds (<c>MurdererPointIncrease</c>).
    /// </summary>
    public const int FurtherKillPoints = 3_600;

    /// <summary>
    /// The divisor of the monster level which gives the points a killed monster removes.
    /// </summary>
    public const int MonsterLevelDivisor = 2;

    /// <summary>
    /// The duration (seconds) of each outlaw stage in Season 6.3 (<c>3 hours</c> with <c>PKTIME = 1</c>).
    /// </summary>
    public const int FormerStageSeconds = 3 * 60 * 60;

    /// <summary>
    /// Gets the outlaw state of a points total for the active season.
    /// </summary>
    /// <param name="points">The points total.</param>
    /// <returns>The outlaw state, or <see cref="HeroState.Normal"/> when the total is cleared.</returns>
    public static HeroState GetState(int points) => GetState(points, HnmoSeasonInstance.IsSeasonNine);

    /// <summary>
    /// Gets the outlaw state of a points total for the specified season.
    /// </summary>
    /// <param name="points">The points total.</param>
    /// <param name="isSeasonNine"><c>true</c> for Season 9 rules; <c>false</c> for Season 6.3 / Arena rules.</param>
    /// <returns>The outlaw state, or <see cref="HeroState.Normal"/> when the total is cleared.</returns>
    public static HeroState GetState(int points, bool isSeasonNine) => isSeasonNine
        ? points switch
        {
            >= PlayerKiller2ndStagePoints => HeroState.PlayerKiller2ndStage,
            >= PlayerKiller1stStagePoints => HeroState.PlayerKiller1stStage,
            > 0 => HeroState.PlayerKillWarning,
            _ => HeroState.Normal,
        }
        : points switch
        {
            > 2 * FormerStageSeconds => HeroState.PlayerKiller2ndStage,
            > FormerStageSeconds => HeroState.PlayerKiller1stStage,
            > 0 => HeroState.PlayerKillWarning,
            _ => HeroState.Normal,
        };

    /// <summary>
    /// Gets the lowest points total of an outlaw state for the active season.
    /// </summary>
    /// <param name="state">The outlaw state.</param>
    /// <returns>The lowest total of the state; 0 if it's not an outlaw state.</returns>
    public static int GetMinimumPoints(HeroState state) => GetMinimumPoints(state, HnmoSeasonInstance.IsSeasonNine);

    /// <summary>
    /// Gets the lowest points total of an outlaw state for the specified season.
    /// </summary>
    /// <param name="state">The outlaw state.</param>
    /// <param name="isSeasonNine"><c>true</c> for Season 9 rules; <c>false</c> for Season 6.3 / Arena rules.</param>
    /// <returns>The lowest total of the state; 0 if it's not an outlaw state.</returns>
    public static int GetMinimumPoints(HeroState state, bool isSeasonNine) => isSeasonNine
        ? state switch
        {
            HeroState.PlayerKillWarning => 1,
            HeroState.PlayerKiller1stStage => PlayerKiller1stStagePoints,
            HeroState.PlayerKiller2ndStage => PlayerKiller2ndStagePoints,
            _ => 0,
        }
        : state switch
        {
            HeroState.PlayerKillWarning => 1,
            HeroState.PlayerKiller1stStage => FormerStageSeconds + 1,
            HeroState.PlayerKiller2ndStage => (2 * FormerStageSeconds) + 1,
            _ => 0,
        };

    /// <summary>
    /// Gets the highest points total of an outlaw stage for the specified season before further-kill extensions.
    /// </summary>
    /// <param name="state">The outlaw state.</param>
    /// <param name="killCount">The saved kill count.</param>
    /// <param name="isSeasonNine"><c>true</c> for Season 9 rules; <c>false</c> for Season 6.3 / Arena rules.</param>
    /// <returns>The maximum points of that state.</returns>
    public static int GetMaximumPoints(HeroState state, int killCount, bool isSeasonNine) => isSeasonNine
        ? state switch
        {
            HeroState.PlayerKiller2ndStage => PlayerKiller2ndStagePoints + (FurtherKillPoints * Math.Max(killCount - 2, 1)),
            HeroState.PlayerKiller1stStage => PlayerKiller2ndStagePoints - 1,
            HeroState.PlayerKillWarning => PlayerKiller1stStagePoints - 1,
            _ => 0,
        }
        : state switch
        {
            HeroState.PlayerKiller2ndStage => (3 * FormerStageSeconds) + (FormerStageSeconds * Math.Max(killCount - 3, 0)),
            HeroState.PlayerKiller1stStage => 2 * FormerStageSeconds,
            HeroState.PlayerKillWarning => FormerStageSeconds,
            _ => 0,
        };

    /// <summary>
    /// Gets the points total after a player kill which counts as murder for the active season.
    /// </summary>
    /// <param name="state">The state before the kill.</param>
    /// <param name="points">The points total before the kill.</param>
    /// <returns>The points total after the kill.</returns>
    public static int GetPointsAfterKill(HeroState state, int points)
        => GetPointsAfterKill(state, points, HnmoSeasonInstance.IsSeasonNine);

    /// <summary>
    /// Gets the points total after a player kill which counts as murder for the specified season.
    /// </summary>
    /// <param name="state">The state before the kill.</param>
    /// <param name="points">The points total before the kill.</param>
    /// <param name="isSeasonNine"><c>true</c> for Season 9 rules; <c>false</c> for Season 6.3 / Arena rules.</param>
    /// <returns>The points total after the kill.</returns>
    public static int GetPointsAfterKill(HeroState state, int points, bool isSeasonNine)
    {
        var current = state > HeroState.Normal ? Math.Max(points, 0) : 0;
        if (!isSeasonNine)
        {
            return state switch
            {
                HeroState.PlayerKiller2ndStage => current + FormerStageSeconds,
                HeroState.PlayerKiller1stStage => Math.Max(current, 3 * FormerStageSeconds),
                HeroState.PlayerKillWarning => Math.Max(current, 2 * FormerStageSeconds),
                _ => FormerStageSeconds,
            };
        }

        return state switch
        {
            HeroState.PlayerKiller2ndStage => current + FurtherKillPoints,
            HeroState.PlayerKiller1stStage => Math.Max(current, PlayerKiller2ndStagePoints) + FurtherKillPoints,
            HeroState.PlayerKillWarning => Math.Max(current + WarningPoints, SeasonNineFirstStageEntryPoints),
            _ => WarningPoints,
        };
    }

    /// <summary>
    /// Gets the points which a killed monster removes from an outlaw.
    /// </summary>
    /// <param name="monsterLevel">The level of the killed monster.</param>
    /// <returns>The points to remove.</returns>
    public static int GetMonsterKillPoints(int monsterLevel) => Math.Max(monsterLevel, 0) / MonsterLevelDivisor;

    /// <summary>
    /// Converts a saved outlaw total on login for the active season so existing players never jump stages after an update.
    /// </summary>
    /// <param name="state">The saved outlaw state.</param>
    /// <param name="remaining">The saved remaining time or points.</param>
    /// <param name="killCount">The saved player kill count.</param>
    /// <returns>The normalized points total within <paramref name="state"/> (or 0 if already expired).</returns>
    public static int ConvertFormerRemainingTime(HeroState state, int remaining, int killCount = 0)
        => ConvertFormerRemainingTime(state, remaining, killCount, HnmoSeasonInstance.IsSeasonNine);

    /// <summary>
    /// Converts a saved outlaw total on login for the specified season so existing players keep their saved
    /// stage with a sensible remaining total instead of jumping up or dropping out on login.
    /// </summary>
    /// <param name="state">The saved outlaw state.</param>
    /// <param name="remaining">The saved remaining time or points.</param>
    /// <param name="killCount">The saved player kill count.</param>
    /// <param name="isSeasonNine"><c>true</c> for Season 9 rules; <c>false</c> for Season 6.3 / Arena rules.</param>
    /// <returns>The normalized points total within <paramref name="state"/> (or 0 if already expired).</returns>
    public static int ConvertFormerRemainingTime(HeroState state, int remaining, int killCount, bool isSeasonNine)
    {
        if (state <= HeroState.Normal)
        {
            return remaining;
        }

        if (remaining <= 0)
        {
            return 0;
        }

        var min = GetMinimumPoints(state, isSeasonNine);
        var max = GetMaximumPoints(state, killCount, isSeasonNine);

        if (!isSeasonNine)
        {
            if (remaining < min)
            {
                // Legacy per-stage format (0 < remaining <= 10,800 per stage): add 3h for each lower stage.
                var lowerStages = state - HeroState.PlayerKillWarning;
                var converted = remaining + (lowerStages * FormerStageSeconds);
                return Math.Clamp(converted, min, max);
            }

            // Old unified points total (21,600 / 43,200 / 86,400 scale): clamp into the saved S6 stage so the stage never promotes.
            return Math.Clamp(remaining, min, max);
        }

        if (remaining < min)
        {
            // Legacy stage-only seconds on S9: shift into the saved stage's ReqPoint range so PK1/PK2 stays in stage.
            return Math.Clamp(min + remaining, min, max);
        }

        return Math.Clamp(remaining, min, max);
    }
}
