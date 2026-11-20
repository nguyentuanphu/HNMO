// <copyright file="LegacyImportPreview.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ServerImporter;

using System;
using System.Collections.Generic;

/// <summary>
/// Non-destructive dry-run report produced before applying any changes.
/// </summary>
public sealed class LegacyImportPreview
{
    /// <summary>Gets or sets the detected server version.</summary>
    public LegacyServerVersion DetectedVersion { get; set; }

    /// <summary>Gets or sets human-readable version detection cues.</summary>
    public List<string> VersionSignals { get; set; } = [];

    /// <summary>Gets or sets parsed rate values ready to apply.</summary>
    public LegacyParsedRates? Rates { get; set; }

    /// <summary>Gets or sets parsed drop rules.</summary>
    public List<LegacyDropRule> DropRules { get; set; } = [];

    /// <summary>Gets or sets the contents of boxes (Item_(group,number,level) item bags) whose items exist in HNMO.</summary>
    public List<LegacyDropRule> BoxContents { get; set; } = [];

    /// <summary>Gets or sets the stats of the monsters HNMO has, where they differ from HNMO's.</summary>
    public List<LegacyMonsterStats> MonsterStats { get; set; } = [];

    /// <summary>Gets or sets parsed NPC shop definitions.</summary>
    public List<LegacyShopDefinition> Shops { get; set; } = [];

    /// <summary>Gets or sets parsed event schedules.</summary>
    public List<LegacyEventScheduleEntry> EventSchedules { get; set; } = [];

    /// <summary>Gets or sets parsed Chaos Machine rate overrides.</summary>
    public List<LegacyChaosRateEntry> ChaosRates { get; set; } = [];

    /// <summary>Gets or sets parsed monster spawn definitions.</summary>
    public List<LegacySpawnEntry> Spawns { get; set; } = [];

    /// <summary>Gets or sets the optional legacy MSSQL database snapshot loaded during preview.</summary>
    public LegacyDatabaseSnapshot? DatabaseSnapshot { get; set; }

    /// <summary>Gets or sets per-category counts and notes.</summary>
    public List<LegacyPreviewCategory> Categories { get; set; } = [];

    /// <summary>Gets or sets reasons why specific items, maps, events, or DB records cannot be mapped into HNMO.</summary>
    public List<string> UnmappedWarnings { get; set; } = [];

    /// <summary>Gets total mapped items across all enabled categories.</summary>
    public int TotalMapped => this.Categories.Sum(c => c.MappedCount);

    /// <summary>Gets total skipped items across all enabled categories.</summary>
    public int TotalSkipped => this.Categories.Sum(c => c.SkippedCount);
}
