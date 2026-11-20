// <copyright file="RateTuningConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GameplayRules;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

/// <summary>
/// Configuration for <see cref="RateTuningPlugIn"/> (disabled by default until saved/enabled in Admin Panel).
/// </summary>
public sealed class RateTuningConfiguration
{
    /// <summary>Gets or sets a value indicating whether rate tuning overrides are active.</summary>
    [Display(Name = "Enabled", Description = "Master switch for Jewel of Soul/Life rates, Life option cap, +15 announcement, Zen drop scaling, and class damage balance.")]
    public bool Enabled { get; set; }

    /// <summary>Gets or sets a value indicating whether Jewel of Soul rates override the default handler configuration.</summary>
    public bool OverrideSoulRates { get; set; }

    /// <summary>Gets or sets Jewel of Soul base success percentage (0..100).</summary>
    public int SoulSuccessRatePercent { get; set; } = 50;

    /// <summary>Gets or sets extra percentage added by Luck when upgrading with Jewel of Soul (0..100).</summary>
    public int SoulLuckBonusPercent { get; set; } = 25;

    /// <summary>Gets or sets a value indicating whether Jewel of Life success rate overrides the default handler configuration.</summary>
    public bool OverrideLifeRate { get; set; }

    /// <summary>Gets or sets Jewel of Life success percentage (0..100).</summary>
    public int LifeSuccessRatePercent { get; set; } = 50;

    /// <summary>Gets or sets maximum Jewel of Life option level (4 = +16 / 4% rec, 7 = +28 / 7% rec).</summary>
    public int MaxLifeOptionLevel { get; set; } = 4;

    /// <summary>Gets or sets a value indicating whether high-level Chaos Mix successes are broadcast globally.</summary>
    public bool BroadcastHighLevelChaosMix { get; set; } = true;

    /// <summary>Gets or sets minimum resulting item level that triggers the global Chaos Mix announcement (10..15).</summary>
    public int BroadcastMinChaosMixLevel { get; set; } = 15;

    /// <summary>Gets or sets global monster Zen drop multiplier percentage (10..1000, 100 = 1.0x).</summary>
    public int ZenDropMultiplierPercent { get; set; } = 100;

    /// <summary>
    /// Gets or sets a value indicating whether the zen of a kill comes from <see cref="ZenTable"/> on the maps it lists
    /// (as the servers' IGC_ZenDrop.xml) instead of from the gained experience. The multipliers still apply.
    /// </summary>
    public bool ZenTableEnabled { get; set; }

    /// <summary>Gets or sets a value indicating whether the table's zen is multiplied by the monster's level.</summary>
    public bool ZenTableMultiplyByMonsterLevel { get; set; }

    /// <summary>Gets or sets the chance, per 10,000, that the zen is multiplied by the monster's level.</summary>
    [Range(0, 10000)]
    public int ZenTableMultiplyChancePerTenThousand { get; set; } = 10000;

    /// <summary>Gets or sets the zen a monster drops on each map listed.</summary>
    public List<ZenTableEntry> ZenTable { get; set; } = [];

    /// <summary>
    /// Gets or sets the party EXP of a kill by party size, as the servers have it: the party gets the EXP of the
    /// kill times this percent (160 % for two members ...), shared by level. Empty: HNMO's own rule (every member's
    /// EXP, +5 % per further member).
    /// </summary>
    public List<PartyExpBonusEntry> PartyExpBonuses { get; set; } = [];

    /// <summary>Gets or sets per-class PvP and PvM damage multiplier entries.</summary>
    public List<ClassDamageRateEntry> ClassDamageRates { get; set; } = CreateDefaultClassRates();

    /// <summary>Creates the canonical default class damage rate table for all 7 base character lines.</summary>
    /// <returns>Initialized list of class entries at 100%.</returns>
    public static List<ClassDamageRateEntry> CreateDefaultClassRates() =>
    [
        new() { ClassLine = 0, NameEn = "Dark Wizard / Soul Master / Grand Master", NameVi = "Phù Thủy (DW / SM / GM)" },
        new() { ClassLine = 4, NameEn = "Dark Knight / Blade Knight / Blade Master", NameVi = "Chiến Binh (DK / BK / BM)" },
        new() { ClassLine = 8, NameEn = "Fairy Elf / Muse Elf / High Elf", NameVi = "Tiên Nữ (Elf / ME / HE)" },
        new() { ClassLine = 12, NameEn = "Magic Gladiator / Duel Master", NameVi = "Đấu Sĩ (MG / DM)" },
        new() { ClassLine = 16, NameEn = "Dark Lord / Lord Emperor", NameVi = "Chúa Tể (DL / LE)" },
        new() { ClassLine = 20, NameEn = "Summoner / Bloody Summoner / Dimension Master", NameVi = "Thuật Sĩ (SUM / BS / DimM)" },
        new() { ClassLine = 24, NameEn = "Rage Fighter / Fist Master", NameVi = "Thiết Binh (RF / FM)" },
    ];
}
