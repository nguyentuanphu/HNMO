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

    /// <summary>Gets or sets a value indicating whether Muun add-option period dates (<c>StartDate..EndDate</c>) are enforced (default false).</summary>
    public bool MuunEnforceDatePeriod { get; set; }

    /// <summary>
    /// Gets or sets the energy a Muun level up costs per current level, for levels without an entry in
    /// <see cref="MuunLevelUpEnergyCosts"/> (default 10; a stored configuration without the setting reads 0, which counts
    /// as the default).
    /// </summary>
    public int MuunLevelUpEnergyCost { get; set; } = 10;

    /// <summary>
    /// Gets or sets the energy of each Muun level up: entry 0 from level 1 to 2, entry 1 from 2 to 3, and so on. HNMO's
    /// defaults (10, 20, 30, 40: <see cref="MuunLevelUpEnergyCost"/> times the level) wait for the desktop client's
    /// values from the Season 9 pack.
    /// </summary>
    public List<int>? MuunLevelUpEnergyCosts { get; set; } = [10, 20, 30, 40];

    /// <summary>
    /// Gets or sets the Muun pets (item, rank, option, add option, evolved item). Empty: the built-in table of
    /// <see cref="SeasonNineSystemsCatalog.Muuns"/>, whose values wait for the desktop client's Season 9 pack.
    /// </summary>
    public List<SeasonNineSystemsCatalog.MuunSpec>? MuunPets { get; set; }

    /// <summary>
    /// Gets or sets the Muun options (stat type, values by level, condition). Empty: the built-in table of
    /// <see cref="SeasonNineSystemsCatalog.MuunOptions"/>, whose values wait for the desktop client's Season 9 pack.
    /// </summary>
    public List<SeasonNineSystemsCatalog.MuunOptionSpec>? MuunOptions { get; set; }

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

    /// <summary>
    /// Gets or sets the NPC (monster definition number) at which errtels are mixed; 0: in any safe zone. HNMO's default
    /// until the Season 9 pack's value is known.
    /// </summary>
    public int PentagramMixNpcNumber { get; set; }

    /// <summary>Gets or sets the chance (n/10000) that a monster drops a pentagram; 0 (default): no drops.</summary>
    public int PentagramDropChance { get; set; }

    /// <summary>Gets or sets the chance (n/10000) that a monster drops an errtel; 0 (default): no drops.</summary>
    public int ErrtelDropChance { get; set; }

    /// <summary>Gets or sets the lowest monster level which drops pentagrams and errtels.</summary>
    public int PentagramDropMinimumMonsterLevel { get; set; } = 1;

    /// <summary>Gets or sets the Zen of an errtel level-up, per level the errtel goes to (HNMO default).</summary>
    public int ErrtelLevelUpZenPerLevel { get; set; } = 100_000;

    /// <summary>Gets or sets the success rate (%) of an errtel level-up from level 0 (HNMO default).</summary>
    public int ErrtelLevelUpBaseRatePct { get; set; } = 90;

    /// <summary>Gets or sets how much the level-up success rate (%) falls per level (HNMO default).</summary>
    public int ErrtelLevelUpRateStepPct { get; set; } = 5;

    /// <summary>Gets or sets the lowest level-up success rate (%) (HNMO default).</summary>
    public int ErrtelLevelUpMinimumRatePct { get; set; } = 40;

    /// <summary>Gets or sets the Zen of an errtel rank-up (two errtels of the same kind, element and rank; HNMO default).</summary>
    public int ErrtelRankUpZen { get; set; } = 1_000_000;

    /// <summary>Gets or sets the success rate (%) of an errtel rank-up (HNMO default).</summary>
    public int ErrtelRankUpRatePct { get; set; } = 60;

    /// <summary>Gets or sets the Zen of taking an errtel out of a pentagram (HNMO default).</summary>
    public int ErrtelExtractZen { get; set; } = 100_000;

    /// <summary>Gets or sets the element of each monster (by monster definition number); a monster not listed has none.</summary>
    public List<PentagramMonsterElement> MonsterElements { get; set; } = [];

    /// <summary>Gets or sets the errtel options; empty: the built-in table (see docs/audit/s9-pentagram-design.md).</summary>
    public List<ErrtelOptionSetting> ErrtelOptions { get; set; } = [];

    /// <summary>Gets or sets the errtel extraction rates; empty: the built-in table.</summary>
    public List<ErrtelExtractionRateSetting> ErrtelExtractionRates { get; set; } = [];

    /// <summary>Gets or sets the pentagram grades (usable slots = grade + 2); empty: the built-in table.</summary>
    public List<PentagramGradeSetting> PentagramGrades { get; set; } = [];

    /// <summary>Gets or sets a value indicating whether the 4 Season 9-only skills (618-621) are active.</summary>
    public bool SeasonNineSkillsEnabled { get; set; } = true;

    /// <summary>Gets or sets Shield-Burn (621) target SD reduction percentage (default 25%).</summary>
    public int ShieldBurnSdPercent { get; set; } = 25;

    /// <summary>Gets or sets Spell of Protection (618) defense bonus percentage (default 15%).</summary>
    public int SpellOfProtectionDefensePct { get; set; } = 15;

    /// <summary>Gets or sets a value indicating whether Season 9 Minimap markers are active for the web client.</summary>
    public bool SeasonNineMinimapEnabled { get; set; } = true;
}
