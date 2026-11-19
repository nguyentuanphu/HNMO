// <copyright file="PostgresSharedLoginTicketStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.EntityFramework;

using System.Threading;
using Npgsql;

/// <summary>The shared login tickets in the shared (Season 6.3) database: an unlogged table of hashes.</summary>
/// <param name="connectionString">The connection string of the shared database.</param>
public sealed class PostgresSharedLoginTicketStore(string connectionString) : ISharedLoginTicketStore
{
    private const string CreateTable = """
        CREATE UNLOGGED TABLE IF NOT EXISTS public.hnmo_login_ticket (hash text PRIMARY KEY, login_name text NOT NULL, expires_at timestamptz NOT NULL);
        """;

    private int _tableReady;

    /// <inheritdoc />
    public async ValueTask AddAsync(string hash, string loginName, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default)
    {
        await using var connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM public.hnmo_login_ticket WHERE expires_at <= now();
            INSERT INTO public.hnmo_login_ticket (hash, login_name, expires_at) VALUES (@hash, @login, @expires) ON CONFLICT (hash) DO NOTHING;
            """;
        command.Parameters.AddWithValue("hash", hash);
        command.Parameters.AddWithValue("login", loginName);
        command.Parameters.AddWithValue("expires", expiresAtUtc.UtcDateTime);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<(string LoginName, DateTimeOffset ExpiresAtUtc)?> TakeAsync(string hash, CancellationToken cancellationToken = default)
    {
        await using var connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM public.hnmo_login_ticket WHERE hash = @hash RETURNING login_name, expires_at;";
        command.Parameters.AddWithValue("hash", hash);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return (reader.GetString(0), new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(1), DateTimeKind.Utc)));
    }

    private async ValueTask<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (Interlocked.CompareExchange(ref this._tableReady, 1, 0) == 0)
        {
            await using var create = connection.CreateCommand();
            create.CommandText = CreateTable;
            await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        return connection;
    }
}
