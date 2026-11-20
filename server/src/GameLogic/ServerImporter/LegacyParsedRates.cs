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
}
