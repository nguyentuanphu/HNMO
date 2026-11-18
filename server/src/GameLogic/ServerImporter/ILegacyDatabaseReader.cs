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

/// <summary>Account row read from <c>MEMB_INFO</c> (without reading password plaintext/hashes).</summary>
public sealed class LegacyAccountRow
{
    /// <summary>Gets or sets login name (<c>memb___id</c>).</summary>
    public string LoginName { get; set; } = string.Empty;

    /// <summary>Gets or sets email address (<c>mail_addr</c>).</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Gets or sets account block flag (<c>bloc_code</c>).</summary>
    public bool IsBanned { get; set; }
}

/// <summary>Character row read from <c>Character</c>.</summary>
public sealed class LegacyCharacterRow
{
    /// <summary>Gets or sets owning account login name.</summary>
    public string AccountId { get; set; } = string.Empty;

    /// <summary>Gets or sets character name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets legacy class byte (0, 1, 2, 16, ...).</summary>
    public byte ClassNumber { get; set; }

    /// <summary>Gets or sets character level.</summary>
    public int Level { get; set; }

    /// <summary>Gets or sets remaining level-up stat points.</summary>
    public int LevelUpPoints { get; set; }

    /// <summary>Gets or sets total experience.</summary>
    public long Experience { get; set; }

    /// <summary>Gets or sets base strength.</summary>
    public int Strength { get; set; }

    /// <summary>Gets or sets base agility/dexterity.</summary>
    public int Agility { get; set; }

    /// <summary>Gets or sets base vitality.</summary>
    public int Vitality { get; set; }

    /// <summary>Gets or sets base energy.</summary>
    public int Energy { get; set; }

    /// <summary>Gets or sets base leadership/command.</summary>
    public int Leadership { get; set; }

    /// <summary>Gets or sets inventory zen.</summary>
    public int Money { get; set; }

    /// <summary>Gets or sets current map number.</summary>
    public short MapNumber { get; set; }

    /// <summary>Gets or sets current X coordinate.</summary>
    public byte MapPosX { get; set; }

    /// <summary>Gets or sets current Y coordinate.</summary>
    public byte MapPosY { get; set; }

    /// <summary>Gets or sets raw inventory binary blob.</summary>
    public byte[] InventoryBlob { get; set; } = [];
}

/// <summary>Warehouse row read from <c>warehouse</c>.</summary>
public sealed class LegacyVaultRow
{
    /// <summary>Gets or sets owning account ID.</summary>
    public string AccountId { get; set; } = string.Empty;

    /// <summary>Gets or sets vault zen.</summary>
    public int Money { get; set; }

    /// <summary>Gets or sets raw vault binary blob.</summary>
    public byte[] ItemsBlob { get; set; } = [];
}

/// <summary>Guild row read from <c>Guild</c>.</summary>
public sealed class LegacyGuildRow
{
    /// <summary>Gets or sets guild name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets guild master character name.</summary>
    public string MasterName { get; set; } = string.Empty;

    /// <summary>Gets or sets 32-byte guild emblem bitmap.</summary>
    public byte[] Logo { get; set; } = [];

    /// <summary>Gets or sets guild score.</summary>
    public int Score { get; set; }

    /// <summary>Gets or sets guild notice text.</summary>
    public string Notice { get; set; } = string.Empty;
}

/// <summary>Guild membership row read from <c>GuildMember</c>.</summary>
public sealed class LegacyGuildMemberRow
{
    /// <summary>Gets or sets guild name.</summary>
    public string GuildName { get; set; } = string.Empty;

    /// <summary>Gets or sets member character name.</summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>Gets or sets role rank code (e.g. 128=Master, 64=Assistant, 32=BattleMaster, 0=Normal).</summary>
    public byte Status { get; set; }
}
