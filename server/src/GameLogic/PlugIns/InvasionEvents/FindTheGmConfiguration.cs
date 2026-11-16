// <copyright file="FindTheGmConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.InvasionEvents;

using System.ComponentModel.DataAnnotations;
using HNMO.GameLogic.PlugIns.PeriodicTasks;

/// <summary>
/// Configuration for the Find the GM scheduled event.
/// </summary>
public class FindTheGmConfiguration : PeriodicInvasionConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FindTheGmConfiguration"/> class.
    /// </summary>
    public FindTheGmConfiguration()
    {
        this.TaskDuration = TimeSpan.FromMinutes(15);
        this.PreStartMessageDelay = TimeSpan.FromSeconds(3);
        this.ForceSingleMap = true;
        this.StartMessage = "[{mapName}] Find the GM has started! Talk to the hidden GameMaster first to win!";
        this.EndMessage = "[{mapName}] Find the GM has ended.";
        this.Timetable = PeriodicTaskConfiguration.GenerateTimeSequence(TimeSpan.FromHours(6), new TimeOnly(1, 0)).ToList();
        this.Mobs =
        [
            new(InvasionMonsters.GameMaster, 1, [InvasionMaps.Lorencia, InvasionMaps.Noria, InvasionMaps.Devias], SpawnMapStrategy.RandomMap),
        ];
    }

    /// <summary>Gets or sets the shared HNMO state file name.</summary>
    public string FileName { get; set; } = HnmoStateStore.StateFileName;

    /// <summary>Gets or sets the coin reward per winner.</summary>
    [Range(0, 1000000)]
    public int RewardCoins { get; set; } = 50;

    /// <summary>Gets or sets the coin type awarded to winners.</summary>
    public CashShopCoinType RewardCoinType { get; set; } = CashShopCoinType.WCoinC;

    /// <summary>Gets or sets the zen reward per winner.</summary>
    [Range(0, 2000000000)]
    public int RewardZen { get; set; } = 500000;

    /// <summary>Gets or sets the maximum number of winners per event run.</summary>
    [Range(1, 20)]
    public int MaxWinners { get; set; } = 1;
}
