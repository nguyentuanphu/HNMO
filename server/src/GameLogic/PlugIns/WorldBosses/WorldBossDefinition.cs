// <copyright file="WorldBossDefinition.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.WorldBosses;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// A world boss: a strong monster which appears on a schedule on a map, is announced in the game, on Discord and on
/// the website, and pays rewards by the damage dealt.
/// </summary>
public class WorldBossDefinition
{
    /// <summary>Gets or sets the identifier; the state is kept by it, so it shouldn't change.</summary>
    [Required]
    [StringLength(40, MinimumLength = 1)]
    [RegularExpression("^[A-Za-z0-9_-]+$")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the boss appears.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the English name.</summary>
    [StringLength(60)]
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese name.</summary>
    [StringLength(60)]
    public string NameVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the monster number.</summary>
    [Range(0, 10000)]
    public short MonsterNumber { get; set; }

    /// <summary>Gets or sets the map number.</summary>
    [Range(0, 255)]
    public short MapNumber { get; set; }

    /// <summary>Gets or sets the game server which hosts the boss; -1 is the first game server (the lowest number) of this process.</summary>
    [Range(-1, 255)]
    public int GameServerId { get; set; } = -1;

    /// <summary>Gets or sets the spawn spots as "x,y; x,y"; one of them is picked. Empty picks a random walkable spot of the area.</summary>
    [StringLength(400)]
    public string SpawnSpots { get; set; } = string.Empty;

    /// <summary>Gets or sets the left edge of the spawn area (used without spawn spots).</summary>
    [Range(0, 255)]
    public byte AreaX1 { get; set; }

    /// <summary>Gets or sets the top edge of the spawn area.</summary>
    [Range(0, 255)]
    public byte AreaY1 { get; set; }

    /// <summary>Gets or sets the right edge of the spawn area.</summary>
    [Range(0, 255)]
    public byte AreaX2 { get; set; } = 255;

    /// <summary>Gets or sets the bottom edge of the spawn area.</summary>
    [Range(0, 255)]
    public byte AreaY2 { get; set; } = 255;

    /// <summary>Gets or sets how the spawn times are set.</summary>
    public WorldBossScheduleKind ScheduleKind { get; set; }

    /// <summary>Gets or sets the hours between two spawns of <see cref="WorldBossScheduleKind.EveryHours"/>.</summary>
    [Range(1, 24)]
    public int EveryHours { get; set; } = 4;

    /// <summary>Gets or sets the first time of the day of <see cref="WorldBossScheduleKind.EveryHours"/> as "HH:mm".</summary>
    [StringLength(5)]
    public string StartTime { get; set; } = "00:00";

    /// <summary>Gets or sets the times of <see cref="WorldBossScheduleKind.FixedTimes"/> as "HH:mm, HH:mm".</summary>
    [StringLength(200)]
    public string FixedTimes { get; set; } = "20:00";

    /// <summary>Gets or sets the days of the week when the boss appears; empty means every day.</summary>
    public ICollection<DayOfWeek> Days { get; set; } = new List<DayOfWeek>();

    /// <summary>Gets or sets how many minutes before the spawn it's announced (0: no announcement before).</summary>
    [Range(0, 240)]
    public int AnnounceMinutesBefore { get; set; } = 10;

    /// <summary>Gets or sets after how many minutes the boss leaves when it wasn't killed.</summary>
    [Range(1, 1440)]
    public int DespawnMinutes { get; set; } = 30;

    /// <summary>Gets or sets the English text announced before the spawn; {boss}, {map} and {minutes} are filled in.</summary>
    [StringLength(200)]
    public string SoonTextEn { get; set; } = "{boss} appears in {map} in {minutes} minutes!";

    /// <summary>Gets or sets the Vietnamese text announced before the spawn.</summary>
    [StringLength(200)]
    public string SoonTextVi { get; set; } = "{boss} sẽ xuất hiện ở {map} sau {minutes} phút!";

    /// <summary>Gets or sets the English text announced at the spawn; {boss}, {map}, {x}, {y} and {minutes} (until it leaves) are filled in.</summary>
    [StringLength(200)]
    public string SpawnTextEn { get; set; } = "{boss} has appeared in {map} ({x}, {y})! It leaves in {minutes} minutes.";

    /// <summary>Gets or sets the Vietnamese text announced at the spawn.</summary>
    [StringLength(200)]
    public string SpawnTextVi { get; set; } = "{boss} đã xuất hiện ở {map} ({x}, {y})! Boss rời đi sau {minutes} phút.";

    /// <summary>Gets or sets the English text announced at the kill; {boss}, {map}, {killer} and {top} (the best damage dealer) are filled in.</summary>
    [StringLength(200)]
    public string KillTextEn { get; set; } = "{boss} was defeated in {map}! Last hit: {killer}, most damage: {top}.";

    /// <summary>Gets or sets the Vietnamese text announced at the kill.</summary>
    [StringLength(200)]
    public string KillTextVi { get; set; } = "{boss} đã bị hạ gục ở {map}! Đòn cuối: {killer}, sát thương cao nhất: {top}.";

    /// <summary>Gets or sets the English text when the boss leaves without being killed.</summary>
    [StringLength(200)]
    public string EscapeTextEn { get; set; } = "{boss} has left {map}.";

    /// <summary>Gets or sets the Vietnamese text when the boss leaves.</summary>
    [StringLength(200)]
    public string EscapeTextVi { get; set; } = "{boss} đã rời khỏi {map}.";

    /// <summary>Gets or sets what the damage ranking counts.</summary>
    public WorldBossRankBy RankBy { get; set; }

    /// <summary>Gets or sets the reward of rank 1.</summary>
    public MissionReward Top1 { get; set; } = new();

    /// <summary>Gets or sets the reward of rank 2.</summary>
    public MissionReward Top2 { get; set; } = new();

    /// <summary>Gets or sets the reward of rank 3.</summary>
    public MissionReward Top3 { get; set; } = new();

    /// <summary>Gets or sets the reward of the ranks 4 to 10.</summary>
    public MissionReward Top10 { get; set; } = new();

    /// <summary>Gets or sets the reward of everyone else who did at least the minimum damage.</summary>
    public MissionReward Participation { get; set; } = new();

    /// <summary>Gets or sets a value indicating whether the top 10 get the participation reward too.</summary>
    public bool TopAlsoGetParticipation { get; set; }

    /// <summary>Gets or sets the share of all damage (in percent) which earns the participation reward.</summary>
    [Range(0, 100)]
    public double ParticipationMinimumPercent { get; set; } = 1;

    /// <summary>Gets or sets the damage which earns the participation reward, whatever the share (0: only the share counts).</summary>
    [Range(0, long.MaxValue)]
    public long ParticipationMinimumDamage { get; set; }

    /// <summary>Gets or sets the bonus of the player with the last hit.</summary>
    public MissionReward LastHit { get; set; } = new();

    /// <summary>
    /// Gets or sets the boss-hunt points of a kill, shared by the damage (each player who did damage gets at least 1).
    /// </summary>
    [Range(0, 100_000)]
    public int HuntPoints { get; set; } = 10;
}
