// <copyright file="GuildLevelDefinition.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Guilds;

using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// A guild level: the experience it needs (in total) and the small buff the online members get from it.
/// </summary>
public class GuildLevelDefinition
{
    /// <summary>Gets or sets the level, from 1.</summary>
    [Range(1, 1000)]
    public int Level { get; set; } = 1;

    /// <summary>Gets or sets the guild experience in total needed for the level.</summary>
    [Range(0, long.MaxValue)]
    public long Xp { get; set; }

    /// <summary>Gets or sets the bonus experience of the members, in percent.</summary>
    [Range(0, 1000)]
    public double ExperiencePercent { get; set; }

    /// <summary>Gets or sets the bonus of the members' zen drops, in percent.</summary>
    [Range(0, 1000)]
    public double ZenPercent { get; set; }

    /// <summary>Gets or sets the bonus defense of the members.</summary>
    [Range(0, 100_000)]
    public int Defense { get; set; }

    /// <summary>Gets a value indicating whether the level gives a buff.</summary>
    [JsonIgnore]
    public bool HasBuff => this.ExperiencePercent > 0 || this.ZenPercent > 0 || this.Defense > 0;
}
