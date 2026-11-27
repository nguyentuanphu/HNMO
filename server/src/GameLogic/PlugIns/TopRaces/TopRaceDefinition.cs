// <copyright file="TopRaceDefinition.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TopRaces;

using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// A top race.
/// </summary>
public class TopRaceDefinition
{
    /// <summary>Gets or sets the identifier; the standings are kept by it, so it shouldn't change while it runs.</summary>
    [Required]
    [StringLength(40, MinimumLength = 1)]
    [RegularExpression("^[A-Za-z0-9_-]+$")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the race runs.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the English title.</summary>
    [StringLength(80)]
    public string TitleEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese title.</summary>
    [StringLength(80)]
    public string TitleVi { get; set; } = string.Empty;

    /// <summary>Gets or sets what it counts.</summary>
    public TopRaceKind Kind { get; set; }

    /// <summary>Gets or sets which events count (event wins only).</summary>
    public TopRaceEvent Event { get; set; }

    /// <summary>Gets or sets who takes part (not for a guild race).</summary>
    public TopRaceScope Scope { get; set; }

    /// <summary>Gets or sets the class of <see cref="TopRaceScope.OneClass"/>.</summary>
    public TopRaceClass Class { get; set; }

    /// <summary>Gets or sets when it runs.</summary>
    public TopRacePeriod Period { get; set; } = TopRacePeriod.Weekly;

    /// <summary>Gets or sets the start of a custom race, "yyyy-MM-dd HH:mm" on the wall clock of the time zone (Sydney).</summary>
    [StringLength(20)]
    public string Start { get; set; } = string.Empty;

    /// <summary>Gets or sets the end of a custom race, "yyyy-MM-dd HH:mm" on the wall clock of the time zone (Sydney).</summary>
    [StringLength(20)]
    public string End { get; set; } = string.Empty;

    /// <summary>Gets or sets the value needed for a prize (at least 1).</summary>
    [Range(1, long.MaxValue)]
    public long Minimum { get; set; } = 1;

    /// <summary>Gets or sets the prizes by rank (for each class's table with <see cref="TopRaceScope.PerClass"/>).</summary>
    public ICollection<TopRacePrize> Prizes { get; set; } = new List<TopRacePrize>();

    /// <summary>Gets the Vietnamese title, or the English one when it's empty.</summary>
    [JsonIgnore]
    public string TitleViOrEn => string.IsNullOrWhiteSpace(this.TitleVi) ? this.TitleEn : this.TitleVi;

    /// <summary>Gets a value indicating whether the race ranks guilds instead of characters.</summary>
    [JsonIgnore]
    public bool IsGuildRace => this.Kind == TopRaceKind.GuildPoints;
}
