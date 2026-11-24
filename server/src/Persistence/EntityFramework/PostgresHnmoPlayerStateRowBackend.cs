// <copyright file="PostgresHnmoPlayerStateRowBackend.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.EntityFramework;

using Npgsql;
using NpgsqlTypes;

/// <summary>
/// PostgreSQL row store for per-account/per-character/per-guild plug-in state (<c>hnmo.player_feature_state</c>)
/// and finished/expired archival (<c>hnmo.archived_feature_state</c>).
/// </summary>
public sealed class PostgresHnmoPlayerStateRowBackend : IHnmoPlayerStateRowQueryBackend
{
    private readonly string _connectionString;
    private readonly object _schemaLock = new();
    private bool _schemaEnsured;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresHnmoPlayerStateRowBackend"/> class.
    /// </summary>
    /// <param name="connectionString">Npgsql connection string.</param>
    public PostgresHnmoPlayerStateRowBackend(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        this._connectionString = connectionString;
    }

    /// <inheritdoc />
    public void EnsureSchema()
    {
        if (this._schemaEnsured)
        {
            return;
        }

        lock (this._schemaLock)
        {
            if (this._schemaEnsured)
            {
                return;
            }

            using var conn = new NpgsqlConnection(this._connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE SCHEMA IF NOT EXISTS hnmo;
                CREATE TABLE IF NOT EXISTS hnmo.player_feature_state (
                    feature TEXT NOT NULL,
                    scope_id TEXT NOT NULL,
                    payload_json JSONB NOT NULL,
                    updated_at_utc TIMESTAMPTZ NOT NULL,
                    expires_at_utc TIMESTAMPTZ NULL,
                    PRIMARY KEY (feature, scope_id)
                );
                CREATE INDEX IF NOT EXISTS ix_hnmo_pfs_expires
                    ON hnmo.player_feature_state (expires_at_utc)
                    WHERE expires_at_utc IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_hnmo_pfs_feature_recent
                    ON hnmo.player_feature_state (feature, updated_at_utc DESC, scope_id);
                CREATE INDEX IF NOT EXISTS ix_hnmo_pfs_activity_item_serials
                    ON hnmo.player_feature_state USING GIN (string_to_array(payload_json ->> 'itemSerials', ','))
                    WHERE feature = 'activityLog.entry';
                CREATE TABLE IF NOT EXISTS hnmo.archived_feature_state (
                    feature TEXT NOT NULL,
                    scope_id TEXT NOT NULL,
                    payload_json JSONB NOT NULL,
                    updated_at_utc TIMESTAMPTZ NOT NULL,
                    archived_at_utc TIMESTAMPTZ NOT NULL
                );
                CREATE INDEX IF NOT EXISTS ix_hnmo_afs_feature_archived
                    ON hnmo.archived_feature_state (feature, archived_at_utc);
                """;
            cmd.ExecuteNonQuery();
            this._schemaEnsured = true;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<HnmoFeatureRow> LoadAll(string feature)
    {
        this.EnsureSchema();
        using var conn = new NpgsqlConnection(this._connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT scope_id, payload_json::text, updated_at_utc, expires_at_utc
            FROM hnmo.player_feature_state
            WHERE feature = @f
            """;
        cmd.Parameters.AddWithValue("f", feature);
        var rows = new List<HnmoFeatureRow>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new HnmoFeatureRow(
                feature,
                reader.GetString(0),
                reader.GetString(1),
                reader.GetDateTime(2).ToUniversalTime(),
                reader.IsDBNull(3) ? null : reader.GetDateTime(3).ToUniversalTime()));
        }

        return rows;
    }

    /// <inheritdoc />
    public HnmoFeatureRow? FindRow(string feature, string scopeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feature);
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeId);
        this.EnsureSchema();
        using var conn = new NpgsqlConnection(this._connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT scope_id, payload_json::text, updated_at_utc, expires_at_utc
            FROM hnmo.player_feature_state
            WHERE feature = @f AND scope_id = @s
            """;
        cmd.Parameters.AddWithValue("f", feature);
        cmd.Parameters.AddWithValue("s", scopeId);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadRow(reader, feature) : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<HnmoFeatureRow> LoadScopePage(string feature, string scopePrefix, int offset, int limit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feature);
        ArgumentNullException.ThrowIfNull(scopePrefix);
        if (offset < 0 || limit is < 1 or > 101)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "A nonnegative offset and bounded row page are required.");
        }

        this.EnsureSchema();
        using var conn = new NpgsqlConnection(this._connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT scope_id, payload_json::text, updated_at_utc, expires_at_utc
            FROM hnmo.player_feature_state
            WHERE feature = @f AND scope_id LIKE @p ESCAPE E'\\'
            ORDER BY updated_at_utc DESC, scope_id
            LIMIT @n OFFSET @skip
            """;
        var pattern = scopePrefix.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%";
        cmd.Parameters.AddWithValue("f", feature);
        cmd.Parameters.AddWithValue("p", pattern);
        cmd.Parameters.AddWithValue("n", limit);
        cmd.Parameters.AddWithValue("skip", offset);
        var rows = new List<HnmoFeatureRow>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(ReadRow(reader, feature));
        }

        return rows;
    }

    /// <inheritdoc />
    public void UpsertBatch(IReadOnlyList<HnmoFeatureRow> upserts, IReadOnlyList<(string Feature, string ScopeId)> deletes)
    {
        if (upserts.Count == 0 && deletes.Count == 0)
        {
            return;
        }

        this.EnsureSchema();
        using var conn = new NpgsqlConnection(this._connectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();
        using var batch = new NpgsqlBatch(conn, tx);

        foreach (var row in upserts)
        {
            var c = new NpgsqlBatchCommand("""
                INSERT INTO hnmo.player_feature_state (feature, scope_id, payload_json, updated_at_utc, expires_at_utc)
                VALUES ($1, $2, $3::jsonb, $4, $5)
                ON CONFLICT (feature, scope_id) DO UPDATE
                SET payload_json = EXCLUDED.payload_json,
                    updated_at_utc = EXCLUDED.updated_at_utc,
                    expires_at_utc = EXCLUDED.expires_at_utc
                """);
            c.Parameters.Add(new NpgsqlParameter { Value = row.Feature });
            c.Parameters.Add(new NpgsqlParameter { Value = row.ScopeId });
            c.Parameters.Add(new NpgsqlParameter { Value = row.PayloadJson });
            c.Parameters.Add(new NpgsqlParameter { Value = DateTime.SpecifyKind(row.UpdatedAtUtc, DateTimeKind.Utc), NpgsqlDbType = NpgsqlDbType.TimestampTz });
            c.Parameters.Add(new NpgsqlParameter { Value = row.ExpiresAtUtc is { } e ? DateTime.SpecifyKind(e, DateTimeKind.Utc) : DBNull.Value, NpgsqlDbType = NpgsqlDbType.TimestampTz });
            batch.BatchCommands.Add(c);
        }

        foreach (var (f, s) in deletes)
        {
            var c = new NpgsqlBatchCommand("DELETE FROM hnmo.player_feature_state WHERE feature = $1 AND scope_id = $2");
            c.Parameters.Add(new NpgsqlParameter { Value = f });
            c.Parameters.Add(new NpgsqlParameter { Value = s });
            batch.BatchCommands.Add(c);
        }

        batch.ExecuteNonQuery();
        tx.Commit();
    }

    /// <inheritdoc />
    public int ArchiveExpired(DateTime cutoffUtc, string? feature = null)
    {
        this.EnsureSchema();
        using var conn = new NpgsqlConnection(this._connectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        var now = DateTime.UtcNow;
        cmd.CommandText = feature is null
            ? """
              WITH moved AS (
                  DELETE FROM hnmo.player_feature_state
                  WHERE expires_at_utc IS NOT NULL AND expires_at_utc <= @cutoff
                  RETURNING feature, scope_id, payload_json, updated_at_utc
              )
              INSERT INTO hnmo.archived_feature_state (feature, scope_id, payload_json, updated_at_utc, archived_at_utc)
              SELECT feature, scope_id, payload_json, updated_at_utc, @now FROM moved;
              """
            : """
              WITH moved AS (
                  DELETE FROM hnmo.player_feature_state
                  WHERE feature = @feature AND expires_at_utc IS NOT NULL AND expires_at_utc <= @cutoff
                  RETURNING feature, scope_id, payload_json, updated_at_utc
              )
              INSERT INTO hnmo.archived_feature_state (feature, scope_id, payload_json, updated_at_utc, archived_at_utc)
              SELECT feature, scope_id, payload_json, updated_at_utc, @now FROM moved;
              """;
        cmd.Parameters.AddWithValue("cutoff", DateTime.SpecifyKind(cutoffUtc, DateTimeKind.Utc));
        cmd.Parameters.AddWithValue("now", DateTime.SpecifyKind(now, DateTimeKind.Utc));
        if (feature is not null)
        {
            cmd.Parameters.AddWithValue("feature", feature);
        }

        var count = cmd.ExecuteNonQuery();
        tx.Commit();
        return count;
    }

    /// <inheritdoc />
    public int CountRows(string? feature = null) => this.CountFromTable("hnmo.player_feature_state", feature);

    /// <inheritdoc />
    public int CountArchivedRows(string? feature = null) => this.CountFromTable("hnmo.archived_feature_state", feature);

    /// <inheritdoc />
    public IReadOnlyList<HnmoFeatureRow> LoadActivityItemPage(Guid serial, DateTime fromUtc, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 101);
        const string feature = "activityLog.entry";
        this.EnsureSchema();
        using var conn = new NpgsqlConnection(this._connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 3;
        cmd.CommandText = """
            SELECT scope_id, payload_json::text, updated_at_utc, expires_at_utc
            FROM hnmo.player_feature_state
            WHERE feature = 'activityLog.entry'
                AND updated_at_utc >= @from
                AND string_to_array(payload_json ->> 'itemSerials', ',') @> ARRAY[@serial]::text[]
            ORDER BY updated_at_utc DESC, scope_id DESC
            LIMIT @limit
            """;
        cmd.Parameters.AddWithValue("serial", serial.ToString("N"));
        cmd.Parameters.AddWithValue("from", DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc));
        cmd.Parameters.AddWithValue("limit", limit);
        using var reader = cmd.ExecuteReader();
        var rows = new List<HnmoFeatureRow>(limit);
        while (reader.Read())
        {
            rows.Add(ReadRow(reader, feature));
        }

        return rows;
    }

    private static HnmoFeatureRow ReadRow(NpgsqlDataReader reader, string feature) => new(
        feature,
        reader.GetString(0),
        reader.GetString(1),
        reader.GetDateTime(2).ToUniversalTime(),
        reader.IsDBNull(3) ? null : reader.GetDateTime(3).ToUniversalTime());

    private int CountFromTable(string table, string? feature)
    {
        this.EnsureSchema();
        using var conn = new NpgsqlConnection(this._connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = feature is null
            ? $"SELECT COUNT(*) FROM {table}"
            : $"SELECT COUNT(*) FROM {table} WHERE feature = @f";
        if (feature is not null)
        {
            cmd.Parameters.AddWithValue("f", feature);
        }

        return Convert.ToInt32(cmd.ExecuteScalar());
    }
}
