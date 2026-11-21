// <copyright file="ILegacyDatabaseReader.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ServerImporter;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Reads legacy MSSQL database tables needed for local server migration.
/// Passwords from legacy tables are intentionally ignored and never read into memory.
/// </summary>
public interface ILegacyDatabaseReader
{
    /// <summary>
    /// Reads accounts, characters, vaults, and guilds from the target database.
    /// </summary>
    /// <param name="connectionString">The database connection string.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The snapshot of legacy database entities.</returns>
    Task<LegacyDatabaseSnapshot> ReadAsync(string connectionString, CancellationToken cancellationToken = default);
}

/// <summary>Snapshot of entities read from a legacy MSSQL database.</summary>
public sealed class LegacyDatabaseSnapshot
{
    /// <summary>Gets or sets accounts.</summary>
    public List<LegacyAccountRow> Accounts { get; set; } = [];

    /// <summary>Gets or sets characters.</summary>
    public List<LegacyCharacterRow> Characters { get; set; } = [];

    /// <summary>Gets or sets vaults.</summary>
    public List<LegacyVaultRow> Vaults { get; set; } = [];

    /// <summary>Gets or sets guilds.</summary>
    public List<LegacyGuildRow> Guilds { get; set; } = [];

    /// <summary>Gets or sets guild memberships.</summary>
    public List<LegacyGuildMemberRow> GuildMembers { get; set; } = [];
}
