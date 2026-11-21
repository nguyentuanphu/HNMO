// <copyright file="MissionDefinition.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Missions;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;

/// <summary>
/// A daily or weekly mission.
/// </summary>
public class MissionDefinition
{
    /// <summary>Gets or sets the identifier; the progress is kept by it, so it shouldn't change.</summary>
    [Required]
    [StringLength(40, MinimumLength = 1)]
    [RegularExpression("^[A-Za-z0-9_-]+$")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the mission is offered.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets when the mission starts again.</summary>
    public MissionPeriod Period { get; set; }

    /// <summary>Gets or sets what the mission counts.</summary>
    public MissionKind Kind { get; set; }

    /// <summary>Gets or sets the count to reach (zen for <see cref="MissionKind.PickUpZen"/>, minutes for <see cref="MissionKind.PlayMinutes"/>).</summary>
    [Range(1, 2_000_000_000)]
    public int Target { get; set; } = 1;

    /// <summary>Gets or sets the monster number which counts for <see cref="MissionKind.KillMonsters"/>; 0 counts every monster.</summary>
    [Range(0, 10000)]
    public short MonsterNumber { get; set; }

    /// <summary>Gets or sets the map number where kills count; -1 counts every map.</summary>
    [Range(-1, 255)]
    public short MapNumber { get; set; } = -1;

    /// <summary>Gets or sets the events which count for the event missions.</summary>
    public MissionEventType EventType { get; set; }

    /// <summary>Gets or sets the English title.</summary>
    [StringLength(120)]
    public string TitleEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese title.</summary>
    [StringLength(120)]
    public string TitleVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the reward.</summary>
    public MissionReward Reward { get; set; } = new();
}
