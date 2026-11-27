// <copyright file="OutlawPoints.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic;

/// <summary>
/// The player killer (outlaw) points, as the Season 6/9 server suites count them: one running total, kept in
/// <see cref="Character.StateRemainingSeconds"/> while the character is an outlaw. A kill raises it, one point goes
/// away per second online and some more with each killed monster, and the outlaw stage follows the total:
/// from <see cref="PlayerKiller2ndStagePoints"/> murderer, from <see cref="PlayerKiller1stStagePoints"/> PK level 2,
/// above 0 Warning, 0 clear.
/// </summary>
public static class OutlawPoints
{
    /// <summary>
    /// The total after the first kill (<see cref="HeroState.PlayerKillWarning"/>).
    /// </summary>
    public const int WarningPoints = 21_600;

    /// <summary>
    /// The threshold of <see cref="HeroState.PlayerKiller1stStage"/> (PK level 2), the total after the second kill.
    /// </summary>
    public const int PlayerKiller1stStagePoints = 43_200;

    /// <summary>
    /// The threshold of <see cref="HeroState.PlayerKiller2ndStage"/> (murderer), the total after the third kill.
    /// </summary>
    public const int PlayerKiller2ndStagePoints = 86_400;

    /// <summary>
    /// The points which every further kill as a murderer adds.
    /// </summary>
    public const int FurtherKillPoints = 3_600;

    /// <summary>
    /// The divisor of the monster level which gives the points a killed monster removes.
    /// </summary>
    public const int MonsterLevelDivisor = 2;

    /// <summary>
    /// The duration of an outlaw stage before the points system, when each stage counted down on its own.
    /// Used to convert the remaining time of characters saved by earlier versions.
    /// </summary>
    public const int FormerStageSeconds = 3 * 60 * 60;

    /// <summary>
    /// Gets the outlaw state of a points total.
    /// </summary>
    /// <param name="points">The points total.</param>
    /// <returns>The outlaw state, or <see cref="HeroState.Normal"/> when the total is cleared.</returns>
    public static HeroState GetState(int points) => points switch
    {
        >= PlayerKiller2ndStagePoints => HeroState.PlayerKiller2ndStage,
        >= PlayerKiller1stStagePoints => HeroState.PlayerKiller1stStage,
        > 0 => HeroState.PlayerKillWarning,
        _ => HeroState.Normal,
    };

    /// <summary>
    /// Gets the lowest points total of an outlaw state.
    /// </summary>
    /// <param name="state">The outlaw state.</param>
    /// <returns>The lowest total of the state; 0 if it's not an outlaw state.</returns>
    public static int GetMinimumPoints(HeroState state) => state switch
    {
        HeroState.PlayerKillWarning => 1,
        HeroState.PlayerKiller1stStage => PlayerKiller1stStagePoints,
        HeroState.PlayerKiller2ndStage => PlayerKiller2ndStagePoints,
        _ => 0,
    };

    /// <summary>
    /// Gets the points total after a player kill which counts as murder: the first kill sets 21,600,
    /// the second 43,200, the third 86,400, and each further kill adds 3,600.
    /// </summary>
    /// <param name="state">The state before the kill.</param>
    /// <param name="points">The points total before the kill.</param>
    /// <returns>The points total after the kill.</returns>
    public static int GetPointsAfterKill(HeroState state, int points)
    {
        var current = state > HeroState.Normal ? Math.Max(points, 0) : 0;
        return state switch
        {
            HeroState.PlayerKiller2ndStage => current + FurtherKillPoints,
            HeroState.PlayerKiller1stStage => Math.Max(current, PlayerKiller2ndStagePoints),
            HeroState.PlayerKillWarning => Math.Max(current, PlayerKiller1stStagePoints),
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
    /// Converts the remaining time of a character saved before the points system, when each outlaw stage counted
    /// down on its own for 3 hours, into the points total: the remaining time of the current stage plus 3 hours for
    /// each lower outlaw stage, so the time until the character is clear stays the same (the stage then follows
    /// the total). A total which already fits the saved state is left as it is.
    /// </summary>
    /// <param name="state">The saved outlaw state.</param>
    /// <param name="remaining">The saved remaining time or points.</param>
    /// <returns>The points total.</returns>
    public static int ConvertFormerRemainingTime(HeroState state, int remaining)
    {
        if (state <= HeroState.Normal || remaining >= GetMinimumPoints(state))
        {
            return remaining;
        }

        var lowerStages = state - HeroState.PlayerKillWarning;
        return Math.Max(remaining, 0) + (lowerStages * FormerStageSeconds);
    }
}
