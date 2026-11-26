// <copyright file="SeasonNineSystemsConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.SeasonNineSystems;

/// <summary>
/// Configuration for <see cref="SeasonNineSystemsPlugIn"/>: Muun pets, Pentagram / Elemental combat,
/// Season 9 skills (618-621), 330-level Master Tree cap, and Season 9 Minimap overlay.
/// </summary>
public sealed class SeasonNineSystemsConfiguration
{
    /// <summary>Gets or sets a value indicating whether Season 9 gameplay systems are enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets a value kept for stored configurations. It changes nothing: the Season 9 systems only ever run in a
    /// Season 9 realm (<c>HNMO_SEASON=s9</c>), never in Season 6.3 or Arena.
    /// </summary>
    public bool SeasonNineOnly { get; set; } = true;

    /// <summary>Gets or sets configured Master Level cap applied when Season 9 systems are active (default 330).</summary>
    public short MasterLevelMaxCap { get; set; } = 330;

    /// <summary>Gets or sets a value indicating whether the Muun companion system (76 pets) is enabled.</summary>
    public bool MuunEnabled { get; set; } = true;

    /// <summary>Gets or sets Muun Energy Converter / evolution point multiplier (default 1.0).</summary>
    public double MuunEvoPointMultiplier { get; set; } = 1.0;

    /// <summary>Gets or sets a value indicating whether Muun map/hour/weekday conditions are enforced.</summary>
    public bool MuunStrictConditions { get; set; } = true;

    /// <summary>
    /// Gets or sets the energy a Muun level up costs per current level (default 10). HNMO's data has no cost table; a
    /// stored configuration without the setting reads 0, which counts as the default.
    /// </summary>
    public int MuunLevelUpEnergyCost { get; set; } = 10;

    /// <summary>Gets or sets Muun drop bonus permille (n/10000, default 50 = 0.5%).</summary>
    public int MuunDropRatePermille { get; set; } = 50;

    /// <summary>Gets or sets a value indicating whether the Pentagram / Elemental combat system is enabled.</summary>
    public bool PentagramEnabled { get; set; } = true;

    /// <summary>Gets or sets elemental damage scaling multiplier (default 1.0).</summary>
    public double PentagramElementalDamageMultiplier { get; set; } = 1.0;

    /// <summary>Gets or sets elemental affinity advantage bonus percentage (default 15%).</summary>
    public int PentagramElementalAdvantageBonusPct { get; set; } = 15;

    /// <summary>Gets or sets flat additive Errtel mix success bonus percentage (default 0).</summary>
    public int PentagramErrtelMixBonusPct { get; set; }

    /// <summary>Gets or sets flat additive Errtel extraction success bonus percentage (default 0).</summary>
    public int PentagramErrtelExtractBonusPct { get; set; }

    /// <summary>Gets or sets a value indicating whether the 4 Season 9-only skills (618-621) are active.</summary>
    public bool SeasonNineSkillsEnabled { get; set; } = true;

    /// <summary>Gets or sets Shield-Burn (621) target SD reduction percentage (default 25%).</summary>
    public int ShieldBurnSdPercent { get; set; } = 25;

    /// <summary>Gets or sets Spell of Protection (618) defense bonus percentage (default 15%).</summary>
    public int SpellOfProtectionDefensePct { get; set; } = 15;

    /// <summary>Gets or sets a value indicating whether Season 9 Minimap markers are active for the web client.</summary>
    public bool SeasonNineMinimapEnabled { get; set; } = true;
}
