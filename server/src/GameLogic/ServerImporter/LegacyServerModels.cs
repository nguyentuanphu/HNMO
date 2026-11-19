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
