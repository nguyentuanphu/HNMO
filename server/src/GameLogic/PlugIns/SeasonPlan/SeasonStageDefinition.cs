// <copyright file="SeasonStageDefinition.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.SeasonPlan;

using System.ComponentModel.DataAnnotations;
using HNMO.GameLogic.PlugIns;

/// <summary>
/// One progression stage in a server's Season Plan.
/// A stage opens automatically when its date is reached OR when its boss/player-level condition is met (or when opened by an admin).
/// </summary>
public sealed class SeasonStageDefinition
{
    /// <summary>Gets or sets unique stage ID.</summary>
    [StringLength(32)]
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets English stage title.</summary>
    [StringLength(80)]
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Gets or sets Vietnamese stage title.</summary>
    [StringLength(80)]
    public string NameVi { get; set; } = string.Empty;

    /// <summary>Gets or sets English description shown on website and F8 window.</summary>
    [StringLength(240)]
    public string DescriptionEn { get; set; } = string.Empty;

    /// <summary>Gets or sets Vietnamese description shown on website and F8 window.</summary>
    [StringLength(240)]
    public string DescriptionVi { get; set; } = string.Empty;

    /// <summary>Gets or sets feature gates unlocked by this stage.</summary>
    public List<SeasonFeatureGate> Unlocks { get; set; } = [];

    /// <summary>Gets or sets optional scheduled UTC time when this stage automatically unlocks.</summary>
    public DateTime? UnlockAtUtc { get; set; }

    /// <summary>Gets or sets optional World Boss ID (e.g. <c>red-dragon</c>, <c>kundun</c>) whose kill unlocks this stage.</summary>
    [StringLength(64)]
    public string RequiredBossId { get; set; } = string.Empty;

    /// <summary>Gets or sets how many kills of <see cref="RequiredBossId"/> are needed (default 1).</summary>
    [Range(0, 1000)]
    public int RequiredBossKills { get; set; } = 1;

    /// <summary>Gets or sets optional target character level that <see cref="RequiredPlayersCount"/> distinct players must reach.</summary>
    [Range(0, 1000)]
    public int RequiredPlayerLevel { get; set; }

    /// <summary>Gets or sets how many distinct players must reach <see cref="RequiredPlayerLevel"/>.</summary>
    [Range(0, 10000)]
    public int RequiredPlayersCount { get; set; }

    /// <summary>Gets or sets optional max lucky/leftover box level unlocked at this stage (for combining leftovers).</summary>
    [Range(0, 15)]
    public int MaxBoxLevel { get; set; }
}
