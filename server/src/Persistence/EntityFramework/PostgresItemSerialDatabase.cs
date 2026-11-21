// <copyright file="PostgresItemSerialDatabase.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.EntityFramework;

using Npgsql;

/// <summary>
/// <see cref="IItemSerialDatabase"/> on PostgreSQL.
/// </summary>
public sealed class PostgresItemSerialDatabase : IItemSerialDatabase
{
    private const string AssignSql = """
        update data."Item" set "Serial" = gen_random_uuid()
        where "Serial" is null
          and "ItemStorageId" is not null
          and "ItemStorageId" not in (select "MerchantStoreId" from config."MonsterDefinition" where "MerchantStoreId" is not null)
        """;

    private const string DuplicatesSql = """
        select i."Serial", i."Id", i."ItemStorageId",
               case when c."Id" is not null then 'inventory' when v."Id" is not null then 'vault' else 'held' end,
               c."Name", coalesce(ca."LoginName", v."LoginName"),
               coalesce(split_part(d."Name", '||', 1), ''), i."Level"
        from data."Item" i
        join (select "Serial" from data."Item" where "Serial" is not null group by "Serial" having count(*) > 1) dup on dup."Serial" = i."Serial"
        left join data."Character" c on c."InventoryId" = i."ItemStorageId"
        left join data."Account" ca on ca."Id" = c."AccountId"
        left join data."Account" v on v."VaultId" = i."ItemStorageId"
        left join config."ItemDefinition" d on d."Id" = i."DefinitionId"
        order by i."Serial", i."Id"
        """;

    private readonly string _connectionString;

    /// <summary>Initializes a new instance of the <see cref="PostgresItemSerialDatabase"/> class.</summary>
    /// <param name="connectionString">The connection string.</param>
    public PostgresItemSerialDatabase(string connectionString)
    {
        this._connectionString = connectionString;
    }

    /// <inheritdoc />
    public int AssignMissing()
    {
        using var connection = new NpgsqlConnection(this._connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(AssignSql, connection) { CommandTimeout = 300 };
        return command.ExecuteNonQuery();
    }

    /// <inheritdoc />
    public IReadOnlyList<DuplicateItemRow> FindDuplicates()
    {
        using var connection = new NpgsqlConnection(this._connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(DuplicatesSql, connection) { CommandTimeout = 300 };
        using var reader = command.ExecuteReader();
        var rows = new List<DuplicateItemRow>();
        while (reader.Read())
        {
            rows.Add(new DuplicateItemRow(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.IsDBNull(2) ? null : reader.GetGuid(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetString(6),
                reader.GetInt16(7)));
        }

        return rows;
    }
}
