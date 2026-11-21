// <copyright file="ChaosRatesConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GameplayRules;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Configuration for <see cref="ChaosRatesPlugIn"/> (ROADMAP 14.4). Off until enabled by host.
/// </summary>
public sealed class ChaosRatesConfiguration
{
    /// <summary>Gets or sets a value indicating whether custom Chaos Machine rates are enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets global multiplier applied to non-fixed Chaos Machine rates (1.0 = normal, 1.5 = +50%).</summary>
    [Range(0.1, 10.0)]
    public double ServerWideMultiplier { get; set; } = 1.0;

    /// <summary>Gets or sets per-recipe overrides.</summary>
    public List<ChaosRecipeOverride> RecipeOverrides { get; set; } = [];
}
