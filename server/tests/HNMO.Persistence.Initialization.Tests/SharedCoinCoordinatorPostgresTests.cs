// <copyright file="SharedCoinCoordinatorPostgresTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using System.IO;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Seasons;
using HNMO.Interfaces;
using HNMO.Persistence.EntityFramework;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

/// <summary>
/// Two-database PostgreSQL integration tests (<c>HNMO_TEST_POSTGRES=1</c>) verifying real
/// <c>PREPARE TRANSACTION</c>, local <see cref="AssetOperation"/> commit, full fee refusal,
/// and locked recovery across canonical (<c>hnmo</c>) and Season 9 (<c>hnmo_s9</c>) databases.
/// </summary>
[TestFixture]
[NonParallelizable]
internal class SharedCoinCoordinatorPostgresTests
{
    private const string Login = "twodbcoin";
    private const string OtherRealmLogin = "twodbother";

    private string _canonicalConn = string.Empty;
    private SeasonNineConnectionSettingProvider? _localSettings;
    private PersistenceContextProvider _localProvider = null!;
    private GameConfiguration _localConfig = null!;
    private ConfigFileDatabaseConnectionStringProvider? _canonicalSettings;
    private PersistenceContextProvider _canonicalProvider = null!;
    private GameConfiguration _canonicalConfig = null!;

    /// <summary>Initializes both canonical and Season 9 databases when <c>HNMO_TEST_POSTGRES=1</c>.</summary>
    /// <returns>A task.</returns>
    [OneTimeSetUp]
    public async Task SetUpDatabasesAsync()
    {
        if (Environment.GetEnvironmentVariable("HNMO_TEST_POSTGRES") != "1")
        {
            Assert.Ignore("Needs two PostgreSQL databases; set HNMO_TEST_POSTGRES=1.");
        }

        HNMO.PlugIns.JsonConverterRegistry.RegisterConverter(new HNMO.Persistence.EntityFramework.Json.BinaryAsHexJsonConverter());

        if (!ConnectionConfigurator.IsInitialized)
        {
            await new ConfigFileDatabaseConnectionStringProvider().InitializeAsync(default).ConfigureAwait(false);
        }

        var baseProvider = new ConfigFileDatabaseConnectionStringProvider();
        this._canonicalSettings = baseProvider;
        using (ConnectionConfigurator.UseTemporarily(baseProvider))
        {
            var canonProvider = new PersistenceContextProvider(new NullLoggerFactory(), null);
            using (await canonProvider.ReCreateDatabaseAsync().ConfigureAwait(false))
            {
            }

            await new VersionSeasonSix.DataInitialization(canonProvider, new NullLoggerFactory()).CreateInitialDataAsync(1, true).ConfigureAwait(false);
            this._canonicalConn = PersistenceContextProvider.ResolveConnectionString()!;
            this._canonicalProvider = canonProvider;
            this._canonicalConfig = (GameConfiguration)await new GameConfigurationDataSource(new NullLogger<GameConfigurationDataSource>(), canonProvider).GetOwnerAsync().ConfigureAwait(false);
        }

        this._localSettings = new SeasonNineConnectionSettingProvider(baseProvider);
        using (ConnectionConfigurator.UseTemporarily(this._localSettings))
        {
            this._localProvider = new PersistenceContextProvider(new NullLoggerFactory(), null);
            using (await this._localProvider.ReCreateDatabaseAsync().ConfigureAwait(false))
            {
            }

            await new VersionSeasonSix.DataInitialization(this._localProvider, new NullLoggerFactory()).CreateInitialDataAsync(1, true).ConfigureAwait(false);
            this._localConfig = (GameConfiguration)await new GameConfigurationDataSource(new NullLogger<GameConfigurationDataSource>(), this._localProvider).GetOwnerAsync().ConfigureAwait(false);
        }
    }

    private sealed class SeasonNineConnectionSettingProvider(ConfigFileDatabaseConnectionStringProvider inner) : IDatabaseConnectionSettingProvider
    {
        public Task? Initialization => inner.Initialization;

        public Task InitializeAsync(System.Threading.CancellationToken cancellationToken) => inner.InitializeAsync(cancellationToken);

        public ConnectionSetting GetConnectionSetting<TContextType>()
            where TContextType : Microsoft.EntityFrameworkCore.DbContext => this.GetConnectionSetting(typeof(TContextType));

        public ConnectionSetting GetConnectionSetting(Type contextType)
        {
            var src = inner.GetConnectionSetting(contextType);
            return new ConnectionSetting
            {
                ContextTypeName = src.ContextTypeName,
                DatabaseEngine = src.DatabaseEngine,
                ConnectionString = ConfigFileDatabaseConnectionStringProvider.SeasonConnectionString(HnmoSeasonInstance.SeasonNine, src.ConnectionString!),
            };
        }
    }

    /// <summary>Executes commit, full fee refusal, and locked recovery against real PostgreSQL 2PC.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TwoDatabaseCoordinatorCommitsRefusesAndRecoversOnPostgresAsync()
    {
        using var localScope = ConnectionConfigurator.UseTemporarily(this._localSettings!);
        var store = new PostgresSharedAccountStore(this._canonicalConn);

        await using (var conn = new NpgsqlConnection(this._canonicalConn))
        {
            await conn.OpenAsync().ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                DELETE FROM data."Account" WHERE "LoginName" IN (@login, @otherLogin);
                INSERT INTO data."Account" ("Id", "LoginName", "PasswordHash", "SecurityCode", "EMail", "RegistrationDate", "State", "TimeZone", "IsVaultExtended", "IsTemplate", "IsBot", "IsNetworkObservationActive", "VaultPassword", "WCoinC", "WCoinP", "GoblinPoints")
                VALUES
                    (@id, @login, 'x', '', '', now(), 0, 0, false, false, false, false, '', 100, 0, 0),
                    (@otherId, @otherLogin, 'x', '', '', now(), 0, 0, false, false, false, false, '', 100, 0, 0);
                """;
            cmd.Parameters.AddWithValue("id", GuidV7.NewGuid());
            cmd.Parameters.AddWithValue("otherId", GuidV7.NewGuid());
            cmd.Parameters.AddWithValue("login", Login);
            cmd.Parameters.AddWithValue("otherLogin", OtherRealmLogin);
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        Guid localAccId;
        using (var local = this._localProvider.CreateNewPlayerContext(this._localConfig))
        {
            if (await local.GetAccountByLoginNameAsync(Login).ConfigureAwait(false) is { } old)
            {
                await local.DeleteAsync(old).ConfigureAwait(false);
                await local.SaveChangesAsync().ConfigureAwait(false);
            }

            var acc = local.CreateNew<Account>();
            acc.LoginName = Login;
            acc.PasswordHash = "x";
            acc.Vault = local.CreateNew<ItemStorage>();
            acc.WCoinC = 100;
            await local.SaveChangesAsync().ConfigureAwait(false);
            localAccId = acc.GetId();
        }

        // 1. Insufficient fee is refused in full.
        using (var local = this._localProvider.CreateNewPlayerContext(this._localConfig))
        {
            var acc = (await local.GetAccountByLoginNameAsync(Login).ConfigureAwait(false))!;
            var refused = await SharedCoinCoordinator.ExecuteAsync(
                store,
                local,
                () => this._localProvider.CreateNewPlayerContext(this._localConfig),
                acc,
                Guid.NewGuid(),
                new CoinBalances(-150, 0, 0),
                "shared-coin-spend",
                "game",
                "h-ins",
                "s9").ConfigureAwait(false);
            Assert.That(refused.Outcome, Is.EqualTo(SharedCoinCommitOutcome.Refused));
        }

        // 2. Valid spend commits canonical PREPARE + local receipt together.
        var opCommit = Guid.NewGuid();
        using (var local = this._localProvider.CreateNewPlayerContext(this._localConfig))
        {
            var acc = (await local.GetAccountByLoginNameAsync(Login).ConfigureAwait(false))!;
            var ok = await SharedCoinCoordinator.ExecuteAsync(
                store,
                local,
                () => this._localProvider.CreateNewPlayerContext(this._localConfig),
                acc,
                opCommit,
                new CoinBalances(-40, 0, 0),
                "shared-coin-spend",
                "game",
                "h-40",
                "s9").ConfigureAwait(false);
            Assert.That(ok.Outcome, Is.EqualTo(SharedCoinCommitOutcome.Committed));
        }

        // 3. Prepared orphan without local receipt is rolled back by recovery, while prepared tx for other realm is untouched.
        var orphanGid = SharedCoinGid.Build("s9", Guid.NewGuid());
        var otherGid = SharedCoinGid.Build("arena", Guid.NewGuid());
        var pOrphan = (await store.BeginPreparedCoinChangeAsync(Login, new CoinBalances(-20, 0, 0), orphanGid, SharedCoinGid.OperationLockKey(orphanGid)).ConfigureAwait(false))!;
        await pOrphan.DisposeAsync().ConfigureAwait(false);
        var pOther = (await store.BeginPreparedCoinChangeAsync(OtherRealmLogin, new CoinBalances(-5, 0, 0), otherGid, SharedCoinGid.OperationLockKey(otherGid)).ConfigureAwait(false))!;
        await pOther.DisposeAsync().ConfigureAwait(false);

        try
        {
            var report = await SharedCoinCoordinator.RecoverAsync(
                store,
                () => this._localProvider.CreateNewPlayerContext(this._localConfig),
                "s9").ConfigureAwait(false);
            Assert.That(report.RolledBack, Is.EqualTo(new[] { orphanGid }));
            Assert.That(await store.ListPreparedTransactionsAsync("arena").ConfigureAwait(false), Does.Contain(otherGid));
        }
        finally
        {
            await store.RollbackPreparedAsync(otherGid).ConfigureAwait(false);
        }

        var sharedFinal = await store.FindAsync(Login).ConfigureAwait(false);
        Assert.That(sharedFinal!.Coins.WCoinC, Is.EqualTo(60));
        using (var verifyLocal = this._localProvider.CreateNewPlayerContext(this._localConfig))
        {
            Assert.That((await verifyLocal.GetAssetOperationAsync(opCommit).ConfigureAwait(false))?.AccountId, Is.EqualTo(localAccId));
        }
    }

    /// <summary>
    /// Recovery after the Season 9 server died at each stage of a spend: after PREPARE (no local receipt: rolled back),
    /// after the local commit (receipt there, canonical still prepared: committed), and with the local database unreadable
    /// (kept prepared, then rolled back once it can be read). Nothing stays in pg_prepared_xacts.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RecoveryResolvesEachStageOfADeadSpendOnPostgresAsync()
    {
        using var localScope = ConnectionConfigurator.UseTemporarily(this._localSettings!);
        var store = new PostgresSharedAccountStore(this._canonicalConn);
        await this.ResetAccountsAsync(100).ConfigureAwait(false);
        Func<IPlayerContext> local = () => this._localProvider.CreateNewPlayerContext(this._localConfig);

        // After PREPARE: the process died before its local commit.
        var afterPrepare = SharedCoinGid.Build("s9", Guid.NewGuid());
        await (await store.BeginPreparedCoinChangeAsync(Login, new CoinBalances(-20, 0, 0), afterPrepare, SharedCoinGid.OperationLockKey(afterPrepare)).ConfigureAwait(false))!.DisposeAsync().ConfigureAwait(false);
        Assert.That(await store.ListPreparedTransactionsAsync("s9").ConfigureAwait(false), Does.Contain(afterPrepare));
        var first = await SharedCoinCoordinator.RecoverAsync(store, local, "s9").ConfigureAwait(false);
        Assert.That(first.RolledBack, Is.EqualTo(new[] { afterPrepare }));
        Assert.That((await store.FindAsync(Login).ConfigureAwait(false))!.Coins.WCoinC, Is.EqualTo(100));

        // After the local commit: the receipt is there, the canonical COMMIT PREPARED never ran.
        var operation = Guid.NewGuid();
        var dying = new CommitDiesStore(store);
        using (var context = local())
        {
            var account = (await context.GetAccountByLoginNameAsync(Login).ConfigureAwait(false))!;
            var result = await SharedCoinCoordinator.ExecuteAsync(dying, context, local, account, operation, new CoinBalances(-30, 0, 0), "shared-coin-spend", "game", "h-30", "s9").ConfigureAwait(false);
            Assert.That(result.Outcome, Is.EqualTo(SharedCoinCommitOutcome.CanonicalCommitDeferred));
        }

        var afterLocalCommit = SharedCoinGid.Build("s9", operation);
        Assert.That(await store.ListPreparedTransactionsAsync("s9").ConfigureAwait(false), Does.Contain(afterLocalCommit));
        var second = await SharedCoinCoordinator.RecoverAsync(store, local, "s9").ConfigureAwait(false);
        Assert.That(second.Committed, Is.EqualTo(new[] { afterLocalCommit }));
        Assert.That((await store.FindAsync(Login).ConfigureAwait(false))!.Coins.WCoinC, Is.EqualTo(70));
        using (var check = local())
        {
            Assert.That(await check.GetAssetOperationAsync(operation).ConfigureAwait(false), Is.Not.Null, "the local receipt");
        }

        // The local database can't be read: the prepared transaction stays until it can.
        var unreadable = SharedCoinGid.Build("s9", Guid.NewGuid());
        await (await store.BeginPreparedCoinChangeAsync(Login, new CoinBalances(-5, 0, 0), unreadable, SharedCoinGid.OperationLockKey(unreadable)).ConfigureAwait(false))!.DisposeAsync().ConfigureAwait(false);
        var third = await SharedCoinCoordinator.RecoverAsync(store, () => throw new InvalidOperationException("local database down"), "s9").ConfigureAwait(false);
        Assert.That(third.KeptPendingLocalUnreadable, Is.EqualTo(new[] { unreadable }), $"lock={third.LockAcquired} committed=[{string.Join(",", third.Committed)}] rolledBack=[{string.Join(",", third.RolledBack)}] skippedActive=[{string.Join(",", third.SkippedActive)}] prepared=[{string.Join(",", await store.ListPreparedTransactionsAsync("s9").ConfigureAwait(false))}]");
        var fourth = await SharedCoinCoordinator.RecoverAsync(store, local, "s9").ConfigureAwait(false);
        Assert.That(fourth.RolledBack, Is.EqualTo(new[] { unreadable }));

        Assert.That(await store.ListPreparedTransactionsAsync("s9").ConfigureAwait(false), Is.Empty);
        Assert.That((await store.FindAsync(Login).ConfigureAwait(false))!.Coins.WCoinC, Is.EqualTo(70));
    }

    /// <summary>
    /// While a prepared transaction of a dead season server holds an account's row, a canonical write of that account's
    /// coins (raw SQL, as the website does, and the game's own save) gives up after lock_timeout instead of hanging.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CanonicalCoinWriteFailsFastWhileAPreparedTransactionHoldsTheRowAsync()
    {
        using var localScope = ConnectionConfigurator.UseTemporarily(this._localSettings!);
        var store = new PostgresSharedAccountStore(this._canonicalConn);
        await this.ResetAccountsAsync(100).ConfigureAwait(false);
        var gid = SharedCoinGid.Build("s9", Guid.NewGuid());
        await (await store.BeginPreparedCoinChangeAsync(Login, new CoinBalances(-10, 0, 0), gid, SharedCoinGid.OperationLockKey(gid)).ConfigureAwait(false))!.DisposeAsync().ConfigureAwait(false);
        var previous = Environment.GetEnvironmentVariable("HNMO_DB_LOCK_TIMEOUT_MS");
        Environment.SetEnvironmentVariable("HNMO_DB_LOCK_TIMEOUT_MS", "1500");
        try
        {
            // Raw SQL on a connection string of the server (the default lock_timeout comes with it).
            var raw = ConnectionConfigurator.WithLockTimeout(new NpgsqlConnectionStringBuilder(this._canonicalConn) { Options = null }.ConnectionString);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            await using (var conn = new NpgsqlConnection(raw))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = """UPDATE data."Account" SET "WCoinC" = "WCoinC" + 1 WHERE "LoginName" = @login""";
                cmd.Parameters.AddWithValue("login", Login);
                var error = Assert.ThrowsAsync<PostgresException>(async () => await cmd.ExecuteNonQueryAsync().ConfigureAwait(false));
                Assert.That(ConnectionConfigurator.IsLockTimeout(error), Is.True, error?.SqlState);
            }

            Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(8)), "gave up instead of waiting without end");

            // The game's own save of the account (Entity Framework) gives up the same way.
            watch.Restart();
            using (ConnectionConfigurator.UseTemporarily(this._canonicalSettings!))
            using (var context = this._canonicalProvider.CreateNewPlayerContext(this._canonicalConfig))
            {
                var account = (await context.GetAccountByLoginNameAsync(Login).ConfigureAwait(false))!;
                account.WCoinC += 5;
                var error = Assert.CatchAsync(async () => await context.SaveChangesAsync().ConfigureAwait(false));
                Assert.That(ConnectionConfigurator.IsLockTimeout(error), Is.True, error?.ToString());
            }

            Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(8)));
        }
        finally
        {
            Environment.SetEnvironmentVariable("HNMO_DB_LOCK_TIMEOUT_MS", previous);
            await store.RollbackPreparedAsync(gid).ConfigureAwait(false);
        }

        Assert.That((await store.FindAsync(Login).ConfigureAwait(false))!.Coins.WCoinC, Is.EqualTo(100), "nothing was written");
    }

    private async Task ResetAccountsAsync(int coins)
    {
        await using (var conn = new NpgsqlConnection(this._canonicalConn))
        {
            await conn.OpenAsync().ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                DELETE FROM data."Account" WHERE "LoginName" = @login;
                INSERT INTO data."Account" ("Id", "LoginName", "PasswordHash", "SecurityCode", "EMail", "RegistrationDate", "State", "TimeZone", "IsVaultExtended", "IsTemplate", "IsBot", "IsNetworkObservationActive", "VaultPassword", "WCoinC", "WCoinP", "GoblinPoints")
                VALUES (@id, @login, 'x', '', '', now(), 0, 0, false, false, false, false, '', @coins, 0, 0);
                """;
            cmd.Parameters.AddWithValue("id", GuidV7.NewGuid());
            cmd.Parameters.AddWithValue("login", Login);
            cmd.Parameters.AddWithValue("coins", coins);
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        using var local = this._localProvider.CreateNewPlayerContext(this._localConfig);
        if (await local.GetAccountByLoginNameAsync(Login).ConfigureAwait(false) is { } old)
        {
            await local.DeleteAsync(old).ConfigureAwait(false);
            await local.SaveChangesAsync().ConfigureAwait(false);
        }

        var acc = local.CreateNew<Account>();
        acc.LoginName = Login;
        acc.PasswordHash = "x";
        acc.Vault = local.CreateNew<ItemStorage>();
        acc.WCoinC = coins;
        await local.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>The canonical store of a season server that dies right after its local commit (COMMIT PREPARED never runs).</summary>
    private sealed class CommitDiesStore(PostgresSharedAccountStore inner) : ICoordinatedSharedCoinStore
    {
        public ValueTask<SharedAccount?> FindAsync(string loginName, CancellationToken cancellationToken = default) => inner.FindAsync(loginName, cancellationToken);

        public ValueTask<CoinBalances?> AddCoinsAsync(string loginName, CoinBalances change, CancellationToken cancellationToken = default) => inner.AddCoinsAsync(loginName, change, cancellationToken);

        public ValueTask<PreparedCoinTransaction?> BeginPreparedCoinChangeAsync(string loginName, CoinBalances change, string gid, long operationLockKey, CancellationToken cancellationToken = default)
            => inner.BeginPreparedCoinChangeAsync(loginName, change, gid, operationLockKey, cancellationToken);

        public ValueTask CommitPreparedAsync(string gid, CancellationToken cancellationToken = default) => throw new IOException("the process died before COMMIT PREPARED");

        public ValueTask RollbackPreparedAsync(string gid, CancellationToken cancellationToken = default) => inner.RollbackPreparedAsync(gid, cancellationToken);

        public ValueTask<IReadOnlyList<string>> ListPreparedTransactionsAsync(string realmId, CancellationToken cancellationToken = default) => inner.ListPreparedTransactionsAsync(realmId, cancellationToken);

        public ValueTask<IAsyncDisposable?> TryAcquireRecoveryLockAsync(string realmId, CancellationToken cancellationToken = default) => inner.TryAcquireRecoveryLockAsync(realmId, cancellationToken);

        public ValueTask<IAsyncDisposable?> TryLockOperationAsync(long operationLockKey, CancellationToken cancellationToken = default) => inner.TryLockOperationAsync(operationLockKey, cancellationToken);
    }
}
