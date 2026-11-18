// <copyright file="ChaosRecipeOverride.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GameplayRules;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Per-recipe Chaos Machine success rate override (ROADMAP 14.4).
/// </summary>
public sealed class ChaosRecipeOverride
{
    /// <summary>Gets or sets the recipe number matching <c>ItemCrafting.Number</c>.</summary>
    public byte RecipeNumber { get; set; }

    /// <summary>Gets or sets display name for admin clarity.</summary>
    [StringLength(80)]
    public string RecipeName { get; set; } = string.Empty;

    /// <summary>Gets or sets optional explicit base success rate (0..100); <c>null</c> keeps normal calculated rate.</summary>
    [Range(0, 100)]
    public byte? SuccessRateOverridePercent { get; set; }

    /// <summary>Gets or sets optional per-recipe multiplier (0.1..10.0).</summary>
    [Range(0.1, 10.0)]
    public double RateMultiplier { get; set; } = 1.0;
}
