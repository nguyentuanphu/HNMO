// <copyright file="CombatLimitsConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GameplayRules;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Configuration for <see cref="CombatLimitsPlugIn"/> (ROADMAP 14.3). Off until enabled by host.
/// </summary>
public sealed class CombatLimitsConfiguration
{
    /// <summary>Gets or sets a value indicating whether combat damage caps and reductions are enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets maximum damage per hit (0 = unlimited).</summary>
    [Range(0, 100000000)]
    public int MaxDamagePerHit { get; set; }

    /// <summary>Gets or sets PvP damage reduction percentage (0..95).</summary>
    [Range(0, 95)]
    public int PvpDamageReductionPercent { get; set; }

    /// <summary>Gets or sets Castle Siege damage reduction percentage (0..95) on Valley of Loren (map 30).</summary>
    [Range(0, 95)]
    public int CastleSiegeDamageReductionPercent { get; set; }
}
