// <copyright file="LeftoverRecipe.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.CombiningLeftovers;

using System.ComponentModel.DataAnnotations;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns;

/// <summary>
/// Chaos Machine / bag leftover upgrade recipe (e.g. 2 × Box +2 + JoL + JoC -> Box +3).
/// </summary>
public sealed class LeftoverRecipe
{
    /// <summary>Gets or sets unique recipe ID.</summary>
    [StringLength(32)]
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets English title.</summary>
    [StringLength(80)]
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Gets or sets Vietnamese title.</summary>
    [StringLength(80)]
    public string NameVi { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether this recipe is active.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets success probability percentage (1..100).</summary>
    [Range(1, 100)]
    public int SuccessRatePercent { get; set; } = 100;

    /// <summary>Gets or sets Zen cost.</summary>
    [Range(0, 2_000_000_000)]
    public int ZenCost { get; set; } = 1_000_000;

    /// <summary>Gets or sets resulting item group.</summary>
    [Range(0, 15)]
    public byte OutputGroup { get; set; }

    /// <summary>Gets or sets resulting item number.</summary>
    [Range(0, 511)]
    public short OutputNumber { get; set; }

    /// <summary>Gets or sets resulting item level.</summary>
    [Range(0, 15)]
    public byte OutputLevel { get; set; }

    /// <summary>Gets or sets required season stage box level (0 = always allowed; otherwise blocked until active season box cap reaches this value).</summary>
    [Range(0, 15)]
    public int RequiredStageBoxLevel { get; set; }

    /// <summary>Gets or sets ingredients consumed from the bag.</summary>
    public List<LeftoverIngredient> Inputs { get; set; } = [];
}
