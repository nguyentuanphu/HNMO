// <copyright file="FixedItemOptionRule.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.FixedItemOptions;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Admin rule pinning guaranteed options on matching dropped or event-rewarded items.
/// </summary>
public sealed class FixedItemOptionRule
{
    /// <summary>Gets or sets the rule identifier.</summary>
    [Display(Name = "Id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether this rule is active.</summary>
    [Display(Name = "Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the optional label shown in the admin panel (e.g. "Event Swords Always Luck").</summary>
    [Display(Name = "Label")]
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the target item group (<c>0..15</c>), or <c>-1</c> to match all groups.</summary>
    [Display(Name = "Item Group (-1 = all)")]
    public int ItemGroup { get; set; } = -1;

    /// <summary>Gets or sets the target item number within the group, or <c>-1</c> to match all items in the group.</summary>
    [Display(Name = "Item Number (-1 = all)")]
    public int ItemNumber { get; set; } = -1;

    /// <summary>Gets or sets the source filter: <c>"all"</c>, <c>"drop"</c> (monster drops), or <c>"box_event"</c> (boxes and mini-game rewards).</summary>
    [Display(Name = "Source Filter")]
    public string SourceFilter { get; set; } = "all";

    /// <summary>Gets or sets the optional map number filter (<c>-1</c> = any map).</summary>
    [Display(Name = "Map Number (-1 = any)")]
    public short MapNumber { get; set; } = -1;

    /// <summary>Gets or sets a value indicating whether matching items with a skill definition always receive Skill.</summary>
    [Display(Name = "Pin Skill")]
    public bool PinSkill { get; set; }

    /// <summary>Gets or sets a value indicating whether matching items supporting Luck always receive Luck.</summary>
    [Display(Name = "Pin Luck")]
    public bool PinLuck { get; set; } = true;

    /// <summary>Gets or sets the minimum item upgrade level (<c>0..15</c>).</summary>
    [Display(Name = "Min Level")]
    public byte MinLevel { get; set; }

    /// <summary>Gets or sets the minimum additional option level (<c>0..7</c> for +4..+28, <c>0</c> = ignore).</summary>
    [Display(Name = "Min Option Level")]
    public byte MinOptionLevel { get; set; }

    /// <summary>Gets or sets the minimum number of Excellent options to guarantee (<c>0..6</c>).</summary>
    [Display(Name = "Min Excellent Options")]
    public int MinExcellentCount { get; set; }

    /// <summary>Gets or sets an optional bitmask (<c>1..63</c>) of specific Excellent option numbers to force when supported.</summary>
    [Display(Name = "Forced Excellent Bitmask")]
    public int ForcedExcellentBits { get; set; }
}
