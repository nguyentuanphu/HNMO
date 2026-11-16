// <copyright file="MapRuleEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GameplayRules;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Per-map combat, PK, EXP, item drop, and zen drop rules (ROADMAP 14.1).
/// </summary>
public sealed class MapRuleEntry
{
    /// <summary>Gets or sets the target map number (e.g., 0 = Lorencia).</summary>
    public short MapNumber { get; set; }

    /// <summary>Gets or sets optional display name for the map rule.</summary>
    [StringLength(64)]
    public string MapName { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether PvP attacks are allowed on this map.</summary>
    public bool PvpEnabled { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether killing players incurs PK penalty on this map.</summary>
    public bool PkPenaltyEnabled { get; set; } = true;

    /// <summary>Gets or sets percentage EXP bonus on this map (0 = normal, 100 = +100% / 2x).</summary>
    [Range(-90, 2000)]
    public int ExpBonusPercent { get; set; }

    /// <summary>Gets or sets percentage item drop bonus on this map.</summary>
    [Range(-90, 2000)]
    public int ItemDropBonusPercent { get; set; }

    /// <summary>Gets or sets percentage zen drop bonus on this map.</summary>
    [Range(-90, 2000)]
    public int ZenDropBonusPercent { get; set; }
}
