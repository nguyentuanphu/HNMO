// <copyright file="LegacyParsedRates.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ServerImporter;

using System;
using System.Collections.Generic;

/// <summary>Parsed server EXP and drop rates.</summary>
public sealed class LegacyParsedRates
{
    /// <summary>Gets or sets the normal EXP multiplier.</summary>
    public float? ExperienceRate { get; set; }

    /// <summary>Gets or sets the master EXP multiplier.</summary>
    public float? MasterExperienceRate { get; set; }

    /// <summary>Gets or sets the raw drop percentage (0-100) from server INI if present.</summary>
    public int? ItemDropPercent { get; set; }

    /// <summary>Gets or sets the excellent item drop chance in 1/10000 (ExcItemDropPer) if present.</summary>
    public int? ExcellentItemDropPerTenThousand { get; set; }

    /// <summary>
    /// Gets or sets ZenDropMultipler. It is not a multiplier of all zen, but of the extra zen master-level characters get
    /// (Money += exp / BaseMasterExperience * ZenDropMultipler), so it is shown in the preview only.
    /// </summary>
    public double? ZenMultiplier { get; set; }

    /// <summary>Gets or sets the Jewel of Soul success rate in percent (UseSoulRate).</summary>
    public int? SoulSuccessPercent { get; set; }

    /// <summary>Gets or sets the Jewel of Soul success rate on an item with luck, in percent (UseSoulWithLuckRate).</summary>
    public int? SoulWithLuckSuccessPercent { get; set; }

    /// <summary>Gets or sets the Jewel of Life success rate in percent (UseLifeRate).</summary>
    public int? LifeSuccessPercent { get; set; }

    /// <summary>Gets or sets a value indicating whether the per-map zen table (IGC_ZenDrop.xml) is on.</summary>
    public bool ZenTableEnabled { get; set; }

    /// <summary>Gets or sets a value indicating whether the table's zen is multiplied by the monster's level.</summary>
    public bool ZenTableMultiplyByMonsterLevel { get; set; }

    /// <summary>Gets or sets the chance, per 10,000, of the monster level multiplication.</summary>
    public int ZenTableMultiplyChancePerTenThousand { get; set; } = 10000;

    /// <summary>Gets or sets the zen of each map: map number, minimum, maximum.</summary>
    public List<(short MapNumber, int Minimum, int Maximum)> ZenTable { get; set; } = [];

    /// <summary>Gets or sets the highest normal character level (MaxNormalLevel).</summary>
    public int? MaximumLevel { get; set; }

    /// <summary>Gets or sets the highest master level (MaxMasterLevel).</summary>
    public int? MaximumMasterLevel { get; set; }

    /// <summary>Gets or sets the chance in percent that a dropped item has luck (ItemLuckDrop).</summary>
    public int? ItemLuckDropPercent { get; set; }

    /// <summary>Gets or sets the EXP bonus of maps (IGC_MapAttribute.xml ExpBonus, added to the server's EXP rate), by map number.</summary>
    public Dictionary<short, float> MapExpBonuses { get; set; } = [];

    /// <summary>Gets or sets the zen a PK clear costs per kill (PkClearCost).</summary>
    public int? PkClearCost { get; set; }

    /// <summary>Gets or sets the party EXP bonus in percent by party size (NormalParty2ExpBonus .. NormalParty5ExpBonus).</summary>
    public Dictionary<int, int> PartyExpBonusPercent { get; set; } = [];

    /// <summary>Gets or sets the EXP bonus of a party of different classes, in percent by size (SetParty3ExpBonus ..).</summary>
    public Dictionary<int, int> SetPartyExpBonusPercent { get; set; } = [];

    /// <summary>Gets or sets the level a character needs to create a guild (GuildCreateLevel).</summary>
    public int? GuildCreateLevel { get; set; }

    /// <summary>Gets or sets the most members a guild can have (MaxGuildMember).</summary>
    public int? MaximumGuildMembers { get; set; }
}
