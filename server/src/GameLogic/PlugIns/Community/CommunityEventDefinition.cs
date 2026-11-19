// <copyright file="CommunityEventDefinition.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Community;

using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// A community event: a code word players enter in the game (once per account), or a list of winning characters, and
/// the reward.
/// </summary>
public class CommunityEventDefinition
{
    /// <summary>Gets or sets the identifier; the entries are kept by it, so it shouldn't change.</summary>
    [Required]
    [StringLength(40, MinimumLength = 1)]
    [RegularExpression("^[A-Za-z0-9_-]+$")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the event is on.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the English title.</summary>
    [StringLength(120)]
    public string TitleEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese title.</summary>
    [StringLength(120)]
    public string TitleVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the English description.</summary>
    [StringLength(600)]
    public string DescriptionEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese description.</summary>
    [StringLength(600)]
    public string DescriptionVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the start, in the configured time zone (Sydney), e.g. "2026-11-20 18:00".</summary>
    [StringLength(20)]
    public string Start { get; set; } = string.Empty;

    /// <summary>Gets or sets the end, in the configured time zone (Sydney), e.g. "2026-11-27 23:59".</summary>
    [StringLength(20)]
    public string End { get; set; } = string.Empty;

    /// <summary>Gets or sets the code word players enter while the event runs; empty for none.</summary>
    [StringLength(40)]
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the winning characters, who get the reward once the event started (each once).</summary>
    public ICollection<string> Winners { get; set; } = new List<string>();

    /// <summary>Gets or sets the reward (coins, zen, items).</summary>
    public MissionReward Reward { get; set; } = new() { Coins = 20 };

    /// <summary>Gets or sets a value indicating whether the event is shown on the website.</summary>
    public bool ShowOnWebsite { get; set; } = true;
}
