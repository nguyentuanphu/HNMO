// <copyright file="PostgresSharedAccountStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.EntityFramework;

using System.Threading;
using HNMO.DataModel.Entities;
using Npgsql;

/// <summary>The shared accounts and coins in the shared (Season 6.3) PostgreSQL database.</summary>
/// <param name="connectionString">The connection string of the shared database.</param>
public sealed class PostgresSharedAccountStore(string connectionString) : ISharedAccountStore
{
    /// <inheritdoc />
    public async ValueTask<SharedAccount?> FindAsync(string loginName, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT "LoginName", "PasswordHash", "SecurityCode", "EMail", "State", "LanguageIsoCode", "ChatBanUntil", "WCoinC", "WCoinP", "GoblinPoints"
            FROM data."Account" WHERE lower("LoginName") = lower(@login) AND NOT "IsTemplate" AND NOT "IsBot" LIMIT 1;
            """;
        command.Parameters.AddWithValue("login", loginName);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        async ValueTask<string?> TextAsync(int column) =>
            await reader.IsDBNullAsync(column, cancellationToken).ConfigureAwait(false) ? null : reader.GetString(column);
        return new SharedAccount(
            reader.GetString(0),
            await TextAsync(1).ConfigureAwait(false) ?? string.Empty,
            await TextAsync(2).ConfigureAwait(false) ?? string.Empty,
            await TextAsync(3).ConfigureAwait(false) ?? string.Empty,
            (AccountState)reader.GetInt32(4),
            await TextAsync(5).ConfigureAwait(false),
            await reader.IsDBNullAsync(6, cancellationToken).ConfigureAwait(false) ? null : reader.GetDateTime(6),
            new CoinBalances(reader.GetInt32(7), reader.GetInt32(8), reader.GetInt32(9)));
    }

    /// <inheritdoc />
    public async ValueTask<CoinBalances?> AddCoinsAsync(string loginName, CoinBalances change, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE data."Account"
            SET "WCoinC" = GREATEST(0, "WCoinC" + @c), "WCoinP" = GREATEST(0, "WCoinP" + @p), "GoblinPoints" = GREATEST(0, "GoblinPoints" + @g)
            WHERE lower("LoginName") = lower(@login) AND NOT "IsTemplate" AND NOT "IsBot"
            RETURNING "WCoinC", "WCoinP", "GoblinPoints";
            """;
        command.Parameters.AddWithValue("login", loginName);
        command.Parameters.AddWithValue("c", change.WCoinC);
        command.Parameters.AddWithValue("p", change.WCoinP);
        command.Parameters.AddWithValue("g", change.GoblinPoints);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? new CoinBalances(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2))
            : null;
    }
}
