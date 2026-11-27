// <copyright file="SharedCoinCoordinatorPostgresTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

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
        using (ConnectionConfigurator.UseTemporarily(baseProvider))
        {
            var canonProvider = new PersistenceContextProvider(new NullLoggerFactory(), null);
            using (await canonProvider.ReCreateDatabaseAsync().ConfigureAwait(false))
            {
            }

            await new VersionSeasonSix.DataInitialization(canonProvider, new NullLoggerFactory()).CreateInitialDataAsync(1, true).ConfigureAwait(false);
            this._canonicalConn = PersistenceContextProvider.ResolveConnectionString()!;
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
}
