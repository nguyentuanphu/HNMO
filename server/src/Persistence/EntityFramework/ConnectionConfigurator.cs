// <copyright file="ConnectionConfigurator.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.EntityFramework;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Nito.AsyncEx.Synchronous;
using Npgsql;

/// <summary>
/// The database roles.
/// </summary>
public enum DatabaseRole
{
    /// <summary>
    /// The admin role which can create databases and other roles.
    /// </summary>
    Admin,

    /// <summary>
    /// The account role which can load and save account data.
    /// </summary>
    Account,

    /// <summary>
    /// The role which can load configuration data.
    /// </summary>
    Configuration,

    /// <summary>
    /// The role which can load guild data.
    /// </summary>
    Guild,

    /// <summary>
    /// The role which can load friend data.
    /// </summary>
    Friend,
}

/// <summary>
/// The database connection configurator which loads the configuration from a file.
/// TODO: Make class non-static.
/// </summary>
public static class ConnectionConfigurator
{
    private static IDatabaseConnectionSettingProvider? _provider;

    /// <summary>
    /// Gets a value indicating whether this instance is initialized.
    /// </summary>
    public static bool IsInitialized => _provider is not null;

    private static IDatabaseConnectionSettingProvider Provider => _provider ?? throw new InvalidOperationException("Call Initialize before.");

    /// <summary>
    /// Initializes this instance.
    /// </summary>
    /// <param name="provider">The <see cref="IDatabaseConnectionSettingProvider"/> which provides the required connection settings.</param>
    public static void Initialize(IDatabaseConnectionSettingProvider provider)
    {
        if (_provider is not null)
        {
            throw new InvalidOperationException("provider is initialized already");
        }

        _provider = provider;
    }

    /// <summary>
    /// Gets the name of the role from the configured connection string.
    /// </summary>
    /// <param name="role">The role.</param>
    /// <returns>The name of the role from the configured connection string.</returns>
    public static string GetRoleName(DatabaseRole role)
    {
        Provider.Initialization?.WaitWithoutException();
        var settings = Provider.GetConnectionSetting(GetContextTypeOfRole(role));
        return new NpgsqlConnectionStringBuilder(settings.ConnectionString!).Username ?? string.Empty;
    }

    /// <summary>
    /// Gets the password password of the role from the configured connection string.
    /// </summary>
    /// <param name="role">The role.</param>
    /// <returns>The password password of the role from the configured connection string.</returns>
    public static string GetRolePassword(DatabaseRole role)
    {
        Provider.Initialization?.WaitWithoutException();
        var settings = Provider.GetConnectionSetting(GetContextTypeOfRole(role));
        return new NpgsqlConnectionStringBuilder(settings.ConnectionString!).Password ?? string.Empty;
    }

    /// <summary>
    /// Uses the given provider instead of the current one, until the returned object is disposed.
    /// Only meant for tests which work with a database of their own.
    /// </summary>
    /// <param name="provider">The provider to use meanwhile.</param>
    /// <returns>The object which brings back the previous provider when it's disposed.</returns>
    internal static IDisposable UseTemporarily(IDatabaseConnectionSettingProvider provider)
    {
        var previous = _provider;
        _provider = provider;
        return new Nito.Disposables.Disposable(() => _provider = previous);
    }

    /// <summary>
    /// Gets the configured connection string of the specified context type.
    /// </summary>
    /// <typeparam name="TContext">The type of the context.</typeparam>
    /// <returns>The configured connection string of the specified context type.</returns>
    internal static string GetConnectionString<TContext>()
        where TContext : DbContext
    {
        Provider.Initialization?.WaitWithoutException();
        return WithLockTimeout(Provider.GetConnectionSetting(typeof(TContext)).ConnectionString!);
    }

    /// <summary>The default wait for a row lock of every connection, in ms (0: none).</summary>
    public const int DefaultLockTimeoutMs = 10_000;

    /// <summary>
    /// A connection string whose sessions give up waiting for a lock after a while (lock_timeout), unless it sets one of
    /// its own. Without it a write waited without end behind a row lock nobody releases: a shared-coin transaction
    /// PREPAREd by a season server that died and wasn't started again holds its account row until it is resolved, and
    /// every other write of that account's coins (game, website, admin panel) hung instead of failing with 55P03.
    /// HNMO_DB_LOCK_TIMEOUT_MS sets the wait (0 turns it off).
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>The connection string with the lock timeout.</returns>
    public static string WithLockTimeout(string connectionString)
    {
        var milliseconds = int.TryParse(Environment.GetEnvironmentVariable("HNMO_DB_LOCK_TIMEOUT_MS"), out var configured) && configured >= 0
            ? configured
            : DefaultLockTimeoutMs;
        if (milliseconds == 0 || string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (builder.Options?.Contains("lock_timeout", StringComparison.OrdinalIgnoreCase) == true)
        {
            return connectionString;
        }

        builder.Options = string.IsNullOrWhiteSpace(builder.Options)
            ? $"-c lock_timeout={milliseconds}"
            : $"{builder.Options} -c lock_timeout={milliseconds}";
        return builder.ConnectionString;
    }

    /// <summary>Whether an error is PostgreSQL giving up waiting for a lock (55P03, lock_not_available).</summary>
    /// <param name="exception">The error (or one wrapping it).</param>
    /// <returns><c>true</c>, if it is.</returns>
    public static bool IsLockTimeout(Exception? exception)
    {
        for (var e = exception; e is not null; e = e.InnerException)
        {
            if (e is PostgresException { SqlState: PostgresErrorCodes.LockNotAvailable })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Configures the specified options builder.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="optionsBuilder">The options builder.</param>
    /// <exception cref="NotImplementedException">At the moment only Npgsql engine (PostgreSQL) is implemented.</exception>
    internal static void Configure(this DbContext context, DbContextOptionsBuilder optionsBuilder)
    {
        // see https://github.com/dotnet/efcore/issues/34431
        optionsBuilder.ConfigureWarnings(a => a.Ignore(RelationalEventId.PendingModelChangesWarning));

        // The subclasses of the TypedContext, which just exist to have a compiled model, use its connection settings.
        var type = context is TypedContext ? typeof(TypedContext) : context.GetType();
        if (type.IsGenericType)
        {
            type = type.GetGenericTypeDefinition();
        }

        Provider.Initialization?.WaitWithoutException();
        if (Provider.GetConnectionSetting(type) is { } setting)
        {
            switch (setting.DatabaseEngine)
            {
                case DatabaseEngine.Npgsql:
                    optionsBuilder.UseNpgsql(WithLockTimeout(setting.ConnectionString!));
                    optionsBuilder.ReplaceService<IMigrationsSqlGenerator, MyNpgsqlMigrationsSqlGenerator>();
                    optionsBuilder.ReplaceService<IModelCacheKeyFactory, MyModelCacheKeyFactory>();
                    return;
                default:
                    throw new NotImplementedException("At the moment only Npgsql engine (PostgreSQL) is implemented.");
            }
        }

        throw new ArgumentException($"No configuration found for context type {context.GetType()}", nameof(context));
    }

    private static Type GetContextTypeOfRole(DatabaseRole role)
    {
        return role switch
        {
            DatabaseRole.Account => typeof(AccountContext),
            DatabaseRole.Admin => typeof(EntityDataContext),
            DatabaseRole.Configuration => typeof(ConfigurationContext),
            DatabaseRole.Guild => typeof(GuildContext),
            DatabaseRole.Friend => typeof(FriendContext),
            _ => throw new ArgumentException($"Role {role} unknown."),
        };
    }
}