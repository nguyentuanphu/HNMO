// <copyright file="LegacyServerModels.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ServerImporter;

using System;
using System.Collections.Generic;

/// <summary>
/// Detected season era of a legacy server configuration folder.
/// </summary>
public enum LegacyServerVersion
{
    /// <summary>Unable to determine version reliably.</summary>
    Unknown = 0,

    /// <summary>Season 6 / 6.3 era configuration.</summary>
    Season6 = 6,

    /// <summary>Season 9 Episode 2 era configuration (contains elemental, companion, or battle-core additions).</summary>
    Season9 = 9,
}

/// <summary>
/// Password migration mode for imported accounts. Plaintext or weak legacy hashes are never stored.
/// </summary>
public enum LegacyPasswordMigrationMode
{
    /// <summary>Replaces every password with a cryptographically random token hashed with BCrypt, requiring players to reset on first login.</summary>
    ForceRandomReset = 0,

    /// <summary>Re-hashes an admin-provided temporary migration passphrase with BCrypt into every imported account.</summary>
    UseTemporaryPassphrase = 1,
}

/// <summary>
/// Options controlling a dry-run preview or applied import from a local legacy server folder and optional MSSQL database.
/// </summary>
public sealed class LegacyImportOptions
{
    /// <summary>Gets or sets the local folder path containing legacy server configs (<c>Data/</c> and/or structured XML config folders).</summary>
    public string FolderPath { get; set; } = string.Empty;

    /// <summary>Gets or sets an optional local MSSQL connection string (left blank to skip database entity import).</summary>
    public string MssqlConnectionString { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether EXP and drop rate settings should be imported.</summary>
    public bool ImportRates { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether custom monster/item drop rules should be imported.</summary>
    public bool ImportDropLists { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether NPC merchant shop inventories should be imported.</summary>
    public bool ImportShops { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether event schedules and bonus rate windows should be imported.</summary>
    public bool ImportEventSchedules { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether Chaos Machine mix success rates should be imported.</summary>
    public bool ImportChaosMixRates { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether monster/NPC spawn tables should be imported.</summary>
    public bool ImportMonsterSpawns { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether accounts, characters, inventories, vaults, and guilds should be imported from MSSQL.</summary>
    public bool ImportDatabaseEntities { get; set; }

    /// <summary>Gets or sets how imported account passwords are secured via BCrypt.</summary>
    public LegacyPasswordMigrationMode PasswordMode { get; set; } = LegacyPasswordMigrationMode.ForceRandomReset;

    /// <summary>Gets or sets the temporary passphrase when <see cref="PasswordMode"/> is <see cref="LegacyPasswordMigrationMode.UseTemporaryPassphrase"/>.</summary>
    public string TemporaryPassphrase { get; set; } = string.Empty;
}

/// <summary>
/// Summary of a single category in a dry-run preview or completed import.
/// </summary>
public sealed class LegacyPreviewCategory
{
    /// <summary>Gets or sets the English category title.</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese category title.</summary>
    public string NameVi { get; set; } = string.Empty;

    /// <summary>Gets or sets how many entries map cleanly into HNMO.</summary>
    public int MappedCount { get; set; }

    /// <summary>Gets or sets how many entries cannot be mapped or are skipped.</summary>
    public int SkippedCount { get; set; }

    /// <summary>Gets or sets brief change details (before -> after or counts).</summary>
    public List<string> Details { get; set; } = [];
}

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

/// <summary>Parsed server EXP and drop rates.</summary>
public sealed class LegacyParsedRates
{
    /// <summary>Gets or sets the normal EXP multiplier.</summary>
    public float? ExperienceRate { get; set; }

    /// <summary>Gets or sets the master EXP multiplier.</summary>
    public float? MasterExperienceRate { get; set; }

    /// <summary>Gets or sets the raw drop percentage (0-100) from server INI if present.</summary>
    public int? ItemDropPercent { get; set; }

    /// <summary>Gets or sets the Zen drop multiplier.</summary>
    public double? ZenMultiplier { get; set; }
}

/// <summary>Parsed monster drop entry.</summary>
public sealed class LegacyDropRule
{
    /// <summary>Gets or sets the monster number (-1 for global drop pool).</summary>
    public int MonsterNumber { get; set; }

    /// <summary>Gets or sets minimum monster level.</summary>
    public int MonsterMinLevel { get; set; }

    /// <summary>Gets or sets maximum monster level.</summary>
    public int MonsterMaxLevel { get; set; }

    /// <summary>Gets or sets target map number (-1 for any).</summary>
    public int MapNumber { get; set; }

    /// <summary>Gets or sets item group (0-15).</summary>
    public byte ItemGroup { get; set; }

    /// <summary>Gets or sets item number.</summary>
    public short ItemNumber { get; set; }

    /// <summary>Gets or sets item drop chance ratio (0..1).</summary>
    public double Chance { get; set; }

    /// <summary>Gets or sets minimum item enhancement level.</summary>
    public byte MinItemLevel { get; set; }

    /// <summary>Gets or sets a value indicating whether the drop is excellent.</summary>
    public bool IsExcellent { get; set; }

    /// <summary>Gets or sets a value indicating whether the drop is ancient set.</summary>
    public bool IsAncient { get; set; }

    /// <summary>Gets or sets socket count.</summary>
    public int SocketCount { get; set; }

    /// <summary>Gets or sets Season 9 elemental attribute flag (0 if none).</summary>
    public int ElementalAttribute { get; set; }

    /// <summary>Gets or sets optional companion evolution category (Season 9+ only).</summary>
    public int? CompanionEvolutionGroup { get; set; }
}

/// <summary>Parsed NPC shop entry.</summary>
public sealed class LegacyShopDefinition
{
    /// <summary>Gets or sets the NPC monster number.</summary>
    public int NpcNumber { get; set; }

    /// <summary>Gets or sets the map number.</summary>
    public int MapNumber { get; set; }

    /// <summary>Gets or sets items sold by the merchant.</summary>
    public List<LegacyShopItemEntry> Items { get; set; } = [];
}

/// <summary>Single item slot sold by a merchant.</summary>
public sealed class LegacyShopItemEntry
{
    /// <summary>Gets or sets item group (0-15).</summary>
    public byte Group { get; set; }

    /// <summary>Gets or sets item number.</summary>
    public short Number { get; set; }

    /// <summary>Gets or sets item level (0-15).</summary>
    public byte Level { get; set; }

    /// <summary>Gets or sets durability.</summary>
    public byte Durability { get; set; }

    /// <summary>Gets or sets whether item has skill.</summary>
    public bool Skill { get; set; }

    /// <summary>Gets or sets whether item has luck.</summary>
    public bool Luck { get; set; }

    /// <summary>Gets or sets item option level (0-7).</summary>
    public byte OptionLevel { get; set; }

    /// <summary>Gets or sets excellent option bitmask.</summary>
    public byte ExcellentBits { get; set; }

    /// <summary>Gets or sets Season 9 elemental attribute flag.</summary>
    public int ElementalAttribute { get; set; }
}

/// <summary>Parsed event schedule window.</summary>
public sealed class LegacyEventScheduleEntry
{
    /// <summary>Gets or sets event key identifier.</summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>Gets or sets event name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets whether this event is Season 9-only and unavailable on HNMO's S6 dataset.</summary>
    public bool IsSeason9Only { get; set; }

    /// <summary>Gets or sets optional map number.</summary>
    public short? MapNumber { get; set; }

    /// <summary>Gets or sets comma-separated HH:mm schedule times.</summary>
    public string ScheduleTimes { get; set; } = string.Empty;

    /// <summary>Gets or sets optional EXP bonus percent.</summary>
    public int ExpBonusPercent { get; set; }

    /// <summary>Gets or sets optional drop bonus percent.</summary>
    public int DropBonusPercent { get; set; }

    /// <summary>Gets or sets optional reward description.</summary>
    public string RewardSummary { get; set; } = string.Empty;
}

/// <summary>Parsed Chaos Machine success rate entry.</summary>
public sealed class LegacyChaosRateEntry
{
    /// <summary>Gets or sets source recipe/key name.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets target HNMO crafting name pattern.</summary>
    public string CraftNamePattern { get; set; } = string.Empty;

    /// <summary>Gets or sets target success percentage (0-100).</summary>
    public int SuccessPercent { get; set; }

    /// <summary>Gets or sets optional Luck bonus percent.</summary>
    public int LuckBonusPercent { get; set; }
}

/// <summary>Parsed monster or NPC spawn line from MonsterSetBase text format.</summary>
public sealed class LegacySpawnEntry
{
    /// <summary>Gets or sets group type (0=NPC, 1=Area, 2=Point, 3=Golden, 4=Event, 5=Fortress, 6=Elemental).</summary>
    public int SpawnGroup { get; set; }

    /// <summary>Gets or sets monster/NPC number.</summary>
    public int MonsterNumber { get; set; }

    /// <summary>Gets or sets map number.</summary>
    public int MapNumber { get; set; }

    /// <summary>Gets or sets X1 coordinate.</summary>
    public int X1 { get; set; }

    /// <summary>Gets or sets Y1 coordinate.</summary>
    public int Y1 { get; set; }

    /// <summary>Gets or sets X2 coordinate.</summary>
    public int X2 { get; set; }

    /// <summary>Gets or sets Y2 coordinate.</summary>
    public int Y2 { get; set; }

    /// <summary>Gets or sets direction code.</summary>
    public int Direction { get; set; }

    /// <summary>Gets or sets spawn quantity.</summary>
    public int Quantity { get; set; }
}

/// <summary>Decoded 16-byte item slot from legacy inventory/warehouse binary blobs.</summary>
public sealed class LegacyDecodedItem
{
    /// <summary>Gets or sets slot index in storage.</summary>
    public byte Slot { get; set; }

    /// <summary>Gets or sets item group (0-15).</summary>
    public byte Group { get; set; }

    /// <summary>Gets or sets item number within group.</summary>
    public short Number { get; set; }

    /// <summary>Gets or sets item enhancement level (0-15).</summary>
    public byte Level { get; set; }

    /// <summary>Gets or sets durability.</summary>
    public byte Durability { get; set; }

    /// <summary>Gets or sets a value indicating whether weapon skill is attached.</summary>
    public bool HasSkill { get; set; }

    /// <summary>Gets or sets a value indicating whether luck is attached.</summary>
    public bool HasLuck { get; set; }

    /// <summary>Gets or sets additional option level (0-7).</summary>
    public byte OptionLevel { get; set; }

    /// <summary>Gets or sets excellent flags byte.</summary>
    public byte ExcellentFlags { get; set; }

    /// <summary>Gets or sets ancient flag byte.</summary>
    public byte AncientFlags { get; set; }
}

/// <summary>Result returned after applying a local import.</summary>
public sealed class LegacyImportApplyResult
{
    /// <summary>Gets or sets the created backup identifier used for undo.</summary>
    public string BackupId { get; set; } = string.Empty;

    /// <summary>Gets or sets the backup file path on disk.</summary>
    public string BackupFilePath { get; set; } = string.Empty;

    /// <summary>Gets or sets counts of created or modified entities.</summary>
    public int AppliedCount { get; set; }

    /// <summary>Gets or sets counts of skipped entities.</summary>
    public int SkippedCount { get; set; }

    /// <summary>Gets or sets summary lines for admin display.</summary>
    public List<string> SummaryLines { get; set; } = [];
}
