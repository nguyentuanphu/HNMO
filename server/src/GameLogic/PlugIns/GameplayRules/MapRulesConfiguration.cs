// <copyright file="MapRulesConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GameplayRules;

/// <summary>
/// Configuration for <see cref="MapRulesPlugIn"/> (ROADMAP 14.1). Off until enabled by host.
/// </summary>
public sealed class MapRulesConfiguration
{
    /// <summary>Gets or sets a value indicating whether per-map overrides are enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets configured per-map rules.</summary>
    public List<MapRuleEntry> Rules { get; set; } = [];
}
