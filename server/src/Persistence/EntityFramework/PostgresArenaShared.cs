// <copyright file="PostgresArenaShared.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.EntityFramework;

using System.Collections.Concurrent;
using Npgsql;

/// <summary>
/// <see cref="IArenaShared"/> in a table of the shared (Season 6.3) database, made on first use.
/// </summary>
public sealed class PostgresArenaShared : IArenaShared
{
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS data."HnmoArenaAccount" (
            "Login" text PRIMARY KEY,
            "Points" integer NOT NULL DEFAULT 0,
            "EarnedDay" text NOT NULL DEFAULT '',
            "EarnedToday" integer NOT NULL DEFAULT 0,
            "Rating" integer NULL);
        """;

    private readonly string _connectionString;
    private readonly object _schemaGate = new();
    private readonly ConcurrentDictionary<string, int> _ratings = new(StringComparer.OrdinalIgnoreCase);
    private bool _schemaReady;
    private DateTime _ratingsReadUtc = DateTime.MinValue;

    /// <summary>Initializes a new instance of the <see cref="PostgresArenaShared"/> class.</summary>
    /// <param name="connectionString">The shared database's connection string.</param>
    public PostgresArenaShared(string connectionString)
    {
        this._connectionString = connectionString;
    }

    /// <inheritdoc />
    public int AddPoints(string login, int points, int dailyCap, string dayKey)
    {
        if (points <= 0)
        {
            return 0;
        }

        // The account's row is locked for the step, so the daily cap holds across programs.
        using var connection = this.Open();
        using var transaction = connection.BeginTransaction();
        using (var insert = new NpgsqlCommand("""INSERT INTO data."HnmoArenaAccount" ("Login") VALUES (@login) ON CONFLICT ("Login") DO NOTHING""", connection, transaction))
        {
            insert.Parameters.AddWithValue("login", Key(login));
            insert.ExecuteNonQuery();
        }

        string day;
        int earnedToday;
        using (var read = new NpgsqlCommand("""SELECT "EarnedDay", "EarnedToday" FROM data."HnmoArenaAccount" WHERE "Login" = @login FOR UPDATE""", connection, transaction))
        {
            read.Parameters.AddWithValue("login", Key(login));
            using var reader = read.ExecuteReader();
            reader.Read();
            day = reader.GetString(0);
            earnedToday = reader.GetInt32(1);
        }

        if (day != dayKey)
        {
            earnedToday = 0;
        }

        var added = dailyCap > 0 ? Math.Min(points, Math.Max(0, dailyCap - earnedToday)) : points;
        using (var update = new NpgsqlCommand("""UPDATE data."HnmoArenaAccount" SET "Points" = "Points" + @added, "EarnedDay" = @day, "EarnedToday" = @earned WHERE "Login" = @login""", connection, transaction))
        {
            update.Parameters.AddWithValue("login", Key(login));
            update.Parameters.AddWithValue("added", added);
            update.Parameters.AddWithValue("day", dayKey);
            update.Parameters.AddWithValue("earned", earnedToday + added);
            update.ExecuteNonQuery();
        }

        transaction.Commit();
        return added;
    }

    /// <inheritdoc />
    public bool TrySpendPoints(string login, int points)
    {
        if (points <= 0)
        {
            return true;
        }

        using var connection = this.Open();
        using var command = new NpgsqlCommand("""UPDATE data."HnmoArenaAccount" SET "Points" = "Points" - @points WHERE "Login" = @login AND "Points" >= @points""", connection);
        command.Parameters.AddWithValue("login", Key(login));
        command.Parameters.AddWithValue("points", points);
        return command.ExecuteNonQuery() == 1;
    }

    /// <inheritdoc />
    public void RefundPoints(string login, int points)
    {
        if (points <= 0)
        {
            return;
        }

        using var connection = this.Open();
        const string sql = """
            INSERT INTO data."HnmoArenaAccount" ("Login", "Points") VALUES (@login, @points)
            ON CONFLICT ("Login") DO UPDATE SET "Points" = data."HnmoArenaAccount"."Points" + @points
            """;
        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("login", Key(login));
        command.Parameters.AddWithValue("points", points);
        command.ExecuteNonQuery();
    }

    /// <inheritdoc />
    public int GetPoints(string login)
    {
        using var connection = this.Open();
        using var command = new NpgsqlCommand("""SELECT "Points" FROM data."HnmoArenaAccount" WHERE "Login" = @login""", connection);
        command.Parameters.AddWithValue("login", Key(login));
        return command.ExecuteScalar() is int points ? points : 0;
    }

    /// <inheritdoc />
    public void SetRating(string login, int rating)
    {
        using var connection = this.Open();
        const string sql = """
            INSERT INTO data."HnmoArenaAccount" ("Login", "Rating") VALUES (@login, @rating)
            ON CONFLICT ("Login") DO UPDATE SET "Rating" = @rating
            """;
        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("login", Key(login));
        command.Parameters.AddWithValue("rating", rating);
        command.ExecuteNonQuery();
        this._ratings[Key(login)] = rating;
    }

    /// <inheritdoc />
    public int? GetRating(string login)
    {
        if (DateTime.UtcNow - this._ratingsReadUtc > TimeSpan.FromMinutes(1))
        {
            this._ratingsReadUtc = DateTime.UtcNow;
            try
            {
                using var connection = this.Open();
                using var command = new NpgsqlCommand("""SELECT "Login", "Rating" FROM data."HnmoArenaAccount" WHERE "Rating" IS NOT NULL""", connection);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    this._ratings[reader.GetString(0)] = reader.GetInt32(1);
                }
            }
            catch (NpgsqlException)
            {
                // The last copy stays; the next minute tries again.
            }
        }

        return this._ratings.TryGetValue(Key(login), out var rating) ? rating : null;
    }

    /// <inheritdoc />
    public (Guid Id, string Name)? GuildOf(string login)
    {
        using var connection = this.Open();
        using var command = new NpgsqlCommand(
            """
            SELECT g."Id", g."Name" FROM guild."GuildMember" m
            JOIN guild."Guild" g ON g."Id" = m."GuildId"
            JOIN data."Character" c ON c."Id" = m."Id"
            JOIN data."Account" a ON a."Id" = c."AccountId"
            WHERE lower(a."LoginName") = lower(@login)
            ORDER BY m."Status" DESC, g."Name"
            LIMIT 1
            """,
            connection);
        command.Parameters.AddWithValue("login", login.Trim());
        using var reader = command.ExecuteReader();
        return reader.Read() ? (reader.GetGuid(0), reader.GetString(1)) : null;
    }

    private static string Key(string login) => login.Trim().ToLowerInvariant();

    private NpgsqlConnection Open()
    {
        var connection = new NpgsqlConnection(this._connectionString);
        connection.Open();
        if (!this._schemaReady)
        {
            lock (this._schemaGate)
            {
                if (!this._schemaReady)
                {
                    using var command = new NpgsqlCommand(Schema, connection);
                    command.ExecuteNonQuery();
                    this._schemaReady = true;
                }
            }
        }

        return connection;
    }
}
