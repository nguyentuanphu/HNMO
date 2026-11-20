// <copyright file="OutlawPoints.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic;

/// <summary>
/// The player killer (outlaw) points, as the Season 6/9 server suites count them.
/// The points of the current outlaw stage are kept in <see cref="Character.StateRemainingSeconds"/>:
/// one point goes away per second online and some more with each killed monster.
/// When they run out, the state falls back one stage and starts with the points of that stage.
/// </summary>
public static class OutlawPoints
{
    /// <summary>
    /// The points of the <see cref="HeroState.PlayerKillWarning"/> stage (first kill).
    /// </summary>
    public const int WarningPoints = 21_600;

    /// <summary>
    /// The points of the <see cref="HeroState.PlayerKiller1stStage"/> stage (PK level 2, second kill).
    /// </summary>
    public const int PlayerKiller1stStagePoints = 43_200;

    /// <summary>
    /// The points of the <see cref="HeroState.PlayerKiller2ndStage"/> stage (murderer, third kill).
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
    /// Gets the points with which the outlaw stage starts.
    /// </summary>
    /// <param name="state">The outlaw state.</param>
    /// <returns>The points of the stage; 0 if it's not an outlaw state.</returns>
    public static int GetStagePoints(HeroState state) => state switch
    {
        HeroState.PlayerKillWarning => WarningPoints,
        HeroState.PlayerKiller1stStage => PlayerKiller1stStagePoints,
        HeroState.PlayerKiller2ndStage => PlayerKiller2ndStagePoints,
        _ => 0,
    };

    /// <summary>
    /// Gets the points which a killed monster removes from an outlaw.
    /// </summary>
    /// <param name="monsterLevel">The level of the killed monster.</param>
    /// <returns>The points to remove.</returns>
    public static int GetMonsterKillPoints(int monsterLevel) => Math.Max(monsterLevel, 0) / MonsterLevelDivisor;
}
