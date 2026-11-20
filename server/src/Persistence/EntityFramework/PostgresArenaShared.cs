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
        CREATE TABLE IF NOT EXISTS data."HnmoArenaSchedule" (
            "Kind" text PRIMARY KEY,
            "Days" text NOT NULL DEFAULT '',
            "Times" text NOT NULL DEFAULT '',
            "NextStartUtc" timestamp with time zone NULL,
            "UpdatedUtc" timestamp with time zone NOT NULL);
        CREATE TABLE IF NOT EXISTS data."HnmoArenaGuildXp" (
            "Reference" text PRIMARY KEY,
            "GuildId" uuid NOT NULL,
            "GuildName" text NOT NULL,
            "Xp" integer NOT NULL,
            "Reason" text NOT NULL DEFAULT '',
            "CreatedUtc" timestamp with time zone NOT NULL,
            "ClaimedUtc" timestamp with time zone NULL);
        CREATE TABLE IF NOT EXISTS data."HnmoOnlinePlayer" (
            "Login" text PRIMARY KEY,
            "Character" text NOT NULL,
            "Season" text NOT NULL,
            "ServerId" integer NOT NULL,
            "UpdatedUtc" timestamp with time zone NOT NULL);
        CREATE TABLE IF NOT EXISTS data."HnmoCrossWhisper" (
            "Id" bigserial PRIMARY KEY,
            "From" text NOT NULL,
            "FromSeason" text NOT NULL,
            "ToSeason" text NOT NULL,
            "ToCharacter" text NOT NULL,
            "Text" text NOT NULL,
            "CreatedUtc" timestamp with time zone NOT NULL);
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
    public void PublishSchedule(string kind, string days, string times, DateTime? nextStartUtc)
    {
        using var connection = this.Open();
        using var command = new NpgsqlCommand(
            """
            INSERT INTO data."HnmoArenaSchedule" ("Kind", "Days", "Times", "NextStartUtc", "UpdatedUtc") VALUES (@kind, @days, @times, @next, @now)
            ON CONFLICT ("Kind") DO UPDATE SET "Days" = @days, "Times" = @times, "NextStartUtc" = @next, "UpdatedUtc" = @now
            """,
            connection);
        command.Parameters.AddWithValue("kind", kind);
        command.Parameters.AddWithValue("days", days);
        command.Parameters.AddWithValue("times", times);
        command.Parameters.AddWithValue("next", nextStartUtc is { } next ? DateTime.SpecifyKind(next, DateTimeKind.Utc) : DBNull.Value);
        command.Parameters.AddWithValue("now", DateTime.UtcNow);
        command.ExecuteNonQuery();
    }

    /// <inheritdoc />
    public IReadOnlyList<ArenaScheduleEntry> Schedules()
    {
        using var connection = this.Open();
        using var command = new NpgsqlCommand("""SELECT "Kind", "Days", "Times", "NextStartUtc", "UpdatedUtc" FROM data."HnmoArenaSchedule" WHERE "UpdatedUtc" > @since""", connection);
        command.Parameters.AddWithValue("since", DateTime.UtcNow.AddMinutes(-10));
        using var reader = command.ExecuteReader();
        var result = new List<ArenaScheduleEntry>();
        while (reader.Read())
        {
            result.Add(new ArenaScheduleEntry(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc),
                DateTime.SpecifyKind(reader.GetDateTime(4), DateTimeKind.Utc)));
        }

        return result;
    }

    /// <inheritdoc />
    public void OweGuildXp(string reference, Guid guildId, string guildName, int xp, string reason)
    {
        using var connection = this.Open();
        using var command = new NpgsqlCommand(
            """
            INSERT INTO data."HnmoArenaGuildXp" ("Reference", "GuildId", "GuildName", "Xp", "Reason", "CreatedUtc")
            VALUES (@reference, @guild, @name, @xp, @reason, @now) ON CONFLICT ("Reference") DO NOTHING
            """,
            connection);
        command.Parameters.AddWithValue("reference", reference);
        command.Parameters.AddWithValue("guild", guildId);
        command.Parameters.AddWithValue("name", guildName);
        command.Parameters.AddWithValue("xp", xp);
        command.Parameters.AddWithValue("reason", reason);
        command.Parameters.AddWithValue("now", DateTime.UtcNow);
        command.ExecuteNonQuery();
    }

    /// <inheritdoc />
    public IReadOnlyList<(string Reference, Guid GuildId, string GuildName, int Xp, string Reason)> ClaimGuildXp(int max)
    {
        using var connection = this.Open();
        using var command = new NpgsqlCommand(
            """
            UPDATE data."HnmoArenaGuildXp" SET "ClaimedUtc" = @now
            WHERE "Reference" IN (SELECT "Reference" FROM data."HnmoArenaGuildXp" WHERE "ClaimedUtc" IS NULL ORDER BY "CreatedUtc" LIMIT @max FOR UPDATE SKIP LOCKED)
            RETURNING "Reference", "GuildId", "GuildName", "Xp", "Reason"
            """,
            connection);
        command.Parameters.AddWithValue("now", DateTime.UtcNow);
        command.Parameters.AddWithValue("max", max);
        using var reader = command.ExecuteReader();
        var result = new List<(string, Guid, string, int, string)>();
        while (reader.Read())
        {
            result.Add((reader.GetString(0), reader.GetGuid(1), reader.GetString(2), reader.GetInt32(3), reader.GetString(4)));
        }

        return result;
    }

    /// <inheritdoc />
    public void PublishOnline(string season, int serverId, IReadOnlyList<(string Login, string Character)> online)
    {
        using var connection = this.Open();
        using var transaction = connection.BeginTransaction();
        var now = DateTime.UtcNow;
        foreach (var (login, character) in online)
        {
            using var upsert = new NpgsqlCommand(
                """
                INSERT INTO data."HnmoOnlinePlayer" ("Login", "Character", "Season", "ServerId", "UpdatedUtc") VALUES (@login, @character, @season, @server, @now)
                ON CONFLICT ("Login") DO UPDATE SET "Character" = @character, "Season" = @season, "ServerId" = @server, "UpdatedUtc" = @now
                """,
                connection,
                transaction);
            upsert.Parameters.AddWithValue("login", login.Trim().ToLowerInvariant());
            upsert.Parameters.AddWithValue("character", character);
            upsert.Parameters.AddWithValue("season", season);
            upsert.Parameters.AddWithValue("server", serverId);
            upsert.Parameters.AddWithValue("now", now);
            upsert.ExecuteNonQuery();
        }

        using var remove = new NpgsqlCommand("""DELETE FROM data."HnmoOnlinePlayer" WHERE "Season" = @season AND "ServerId" = @server AND "UpdatedUtc" < @now""", connection, transaction);
        remove.Parameters.AddWithValue("season", season);
        remove.Parameters.AddWithValue("server", serverId);
        remove.Parameters.AddWithValue("now", now);
        remove.ExecuteNonQuery();
        transaction.Commit();
    }

    /// <inheritdoc />
    public IReadOnlyList<OnlinePresence> OnlineElsewhere(string season)
    {
        using var connection = this.Open();
        using var command = new NpgsqlCommand("""SELECT "Login", "Character", "Season", "ServerId" FROM data."HnmoOnlinePlayer" WHERE "Season" <> @season AND "UpdatedUtc" > @since""", connection);
        command.Parameters.AddWithValue("season", season);
        command.Parameters.AddWithValue("since", DateTime.UtcNow.AddMinutes(-2));
        using var reader = command.ExecuteReader();
        var result = new List<OnlinePresence>();
        while (reader.Read())
        {
            result.Add(new OnlinePresence(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3)));
        }

        return result;
    }

    /// <inheritdoc />
    public void SendCrossWhisper(string from, string fromSeason, string toSeason, string toCharacter, string text)
    {
        using var connection = this.Open();
        using var command = new NpgsqlCommand(
            """INSERT INTO data."HnmoCrossWhisper" ("From", "FromSeason", "ToSeason", "ToCharacter", "Text", "CreatedUtc") VALUES (@from, @fromSeason, @toSeason, @to, @text, @now)""",
            connection);
        command.Parameters.AddWithValue("from", from);
        command.Parameters.AddWithValue("fromSeason", fromSeason);
        command.Parameters.AddWithValue("toSeason", toSeason);
        command.Parameters.AddWithValue("to", toCharacter);
        command.Parameters.AddWithValue("text", text);
        command.Parameters.AddWithValue("now", DateTime.UtcNow);
        command.ExecuteNonQuery();
    }

    /// <inheritdoc />
    public IReadOnlyList<CrossWhisper> TakeCrossWhispers(string season)
    {
        using var connection = this.Open();
        using var command = new NpgsqlCommand(
            """
            DELETE FROM data."HnmoCrossWhisper" WHERE "ToSeason" = @season
            RETURNING "Id", "From", "FromSeason", "ToCharacter", "Text", "CreatedUtc"
            """,
            connection);
        command.Parameters.AddWithValue("season", season);
        using var reader = command.ExecuteReader();
        var result = new List<CrossWhisper>();
        var since = DateTime.UtcNow.AddMinutes(-5);
        while (reader.Read())
        {
            if (DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc) > since)
            {
                result.Add(new CrossWhisper(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4)));
            }
        }

        return result;
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
