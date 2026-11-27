// <copyright file="AssetOperationPostgresTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.Persistence.EntityFramework;

/// <summary>
/// The asset operations (<see cref="IPlayerContext.SaveChangesWithAssetOperationAsync"/>) against a real PostgreSQL
/// database: the receipt, the relative balance changes, the delivery and the context's own changes commit together or
/// not at all. They need a database (ConnectionSettings.xml) and run only with <c>HNMO_TEST_POSTGRES=1</c>; the database
/// is created again from the migrations.
/// </summary>
[TestFixture]
[NonParallelizable]
internal class AssetOperationPostgresTests
{
    private const string Login = "assettest";

    private PersistenceContextProvider _provider = null!;

    private GameConfiguration _configuration = null!;

    /// <summary>Creates the database from the migrations and the Season 6 data.</summary>
    /// <returns>A task.</returns>
    [OneTimeSetUp]
    public async Task CreateDatabaseAsync()
    {
        if (Environment.GetEnvironmentVariable("HNMO_TEST_POSTGRES") != "1")
        {
            Assert.Ignore("Needs a PostgreSQL database; set HNMO_TEST_POSTGRES=1.");
        }

        // As the server does at start-up: PostgreSQL delivers binary columns as hex.
        HNMO.PlugIns.JsonConverterRegistry.RegisterConverter(new HNMO.Persistence.EntityFramework.Json.BinaryAsHexJsonConverter());
        var recreate = new PersistenceContextProvider(new NullLoggerFactory(), null);
        using (await recreate.ReCreateDatabaseAsync().ConfigureAwait(false))
        {
        }

        this._provider = new PersistenceContextProvider(new NullLoggerFactory(), null);
        await new VersionSeasonSix.DataInitialization(this._provider, new NullLoggerFactory()).CreateInitialDataAsync(1, true).ConfigureAwait(false);

        // Loaded the way the server loads it.
        var source = new GameConfigurationDataSource(new NullLogger<GameConfigurationDataSource>(), this._provider);
        this._configuration = (GameConfiguration)await source.GetOwnerAsync().ConfigureAwait(false);
    }

    /// <summary>Gives each test a fresh account.</summary>
    /// <returns>A task.</returns>
    [SetUp]
    public async Task CreateAccountAsync()
    {
        using var context = this._provider.CreateNewPlayerContext(this._configuration);
        if (await context.GetAccountByLoginNameAsync(Login).ConfigureAwait(false) is { } old)
        {
            await context.DeleteAsync(old).ConfigureAwait(false);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        var account = context.CreateNew<Account>();
        account.LoginName = Login;
        account.PasswordHash = "hash";
        account.EMail = "before";
        account.Vault = context.CreateNew<ItemStorage>();
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>A committed operation writes the receipt, the balance and the context's own change together.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ACommittedOperationWritesEverythingTogetherAsync()
    {
        var operation = Guid.NewGuid();
        using (var context = this._provider.CreateNewPlayerContext(this._configuration))
        {
            var account = (await context.GetAccountByLoginNameAsync(Login).ConfigureAwait(false))!;
            account.EMail = "after";
            Assert.That(await context.SaveChangesWithAssetOperationAsync(Deposit(operation, account.GetId(), 4)).ConfigureAwait(false), Is.EqualTo(AssetCommitStatus.Committed));
            Assert.That(await context.SaveChangesAsync().ConfigureAwait(false), Is.True, "the changes were accepted: a normal save after it writes nothing twice");
        }

        await this.AssertStateAsync(email: "after", bless: 4, receipt: operation).ConfigureAwait(false);
    }

    /// <summary>A refused operation writes nothing, not even the context's own change, which stays pending.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ARefusedOperationWritesNothingAsync()
    {
        using (var context = this._provider.CreateNewPlayerContext(this._configuration))
        {
            var account = (await context.GetAccountByLoginNameAsync(Login).ConfigureAwait(false))!;
            account.EMail = "after";
            var debit = new AssetOperationRequest(Guid.NewGuid(), account.GetId(), "jewel-withdraw", "w1", "game", null, [new JewelBankDelta("bless", -1, 100)]);
            Assert.That(await context.SaveChangesWithAssetOperationAsync(debit).ConfigureAwait(false), Is.EqualTo(AssetCommitStatus.Refused));
        }

        await this.AssertStateAsync(email: "before", bless: 0, receipt: null).ConfigureAwait(false);
    }

    /// <summary>A retry with the same identifier and payload changes nothing more; another payload is a conflict.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ARetryIsReportedAndAnotherPayloadIsAConflictAsync()
    {
        var operation = Guid.NewGuid();
        Guid accountId;
        using (var context = this._provider.CreateNewPlayerContext(this._configuration))
        {
            accountId = (await context.GetAccountByLoginNameAsync(Login).ConfigureAwait(false))!.GetId();
            Assert.That(await context.SaveChangesWithAssetOperationAsync(Deposit(operation, accountId, 4)).ConfigureAwait(false), Is.EqualTo(AssetCommitStatus.Committed));
        }

        using (var context = this._provider.CreateNewPlayerContext(this._configuration))
        {
            Assert.That(await context.SaveChangesWithAssetOperationAsync(Deposit(operation, accountId, 4)).ConfigureAwait(false), Is.EqualTo(AssetCommitStatus.AlreadyCommitted));
            Assert.That(await context.SaveChangesWithAssetOperationAsync(Deposit(operation, accountId, 5) with { PayloadHash = "other" }).ConfigureAwait(false), Is.EqualTo(AssetCommitStatus.Conflict));
        }

        await this.AssertStateAsync(email: "before", bless: 4, receipt: operation).ConfigureAwait(false);
    }

    /// <summary>A credit over the capacity is refused, a debit below zero too.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TheCapacityAndZeroAreKeptAsync()
    {
        using var context = this._provider.CreateNewPlayerContext(this._configuration);
        var accountId = (await context.GetAccountByLoginNameAsync(Login).ConfigureAwait(false))!.GetId();
        Assert.That(await context.SaveChangesWithAssetOperationAsync(Deposit(Guid.NewGuid(), accountId, 8, capacity: 10)).ConfigureAwait(false), Is.EqualTo(AssetCommitStatus.Committed));
        Assert.That(await context.SaveChangesWithAssetOperationAsync(Deposit(Guid.NewGuid(), accountId, 3, capacity: 10)).ConfigureAwait(false), Is.EqualTo(AssetCommitStatus.Refused));
        Assert.That(await context.SaveChangesWithAssetOperationAsync(Deposit(Guid.NewGuid(), accountId, -9, capacity: 10)).ConfigureAwait(false), Is.EqualTo(AssetCommitStatus.Refused));
        Assert.That((await context.GetJewelBankBalancesAsync(accountId).ConfigureAwait(false))["bless"], Is.EqualTo(8));
    }

    /// <summary>Two debits of 3 from a balance of 5 at the same time: one goes through, the other is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConcurrentDebitsCantTakeMoreThanTheBalanceAsync()
    {
        Guid accountId;
        using (var context = this._provider.CreateNewPlayerContext(this._configuration))
        {
            accountId = (await context.GetAccountByLoginNameAsync(Login).ConfigureAwait(false))!.GetId();
            Assert.That(await context.SaveChangesWithAssetOperationAsync(Deposit(Guid.NewGuid(), accountId, 5)).ConfigureAwait(false), Is.EqualTo(AssetCommitStatus.Committed));
        }

        async Task<AssetCommitStatus> DebitAsync()
        {
            using var context = this._provider.CreateNewPlayerContext(this._configuration);
            return await context.SaveChangesWithAssetOperationAsync(Deposit(Guid.NewGuid(), accountId, -3) with { Kind = "jewel-withdraw" }).ConfigureAwait(false);
        }

        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(DebitAsync))).ConfigureAwait(false);
        Assert.That(results.Count(r => r == AssetCommitStatus.Committed), Is.EqualTo(1));
        Assert.That(results.Count(r => r == AssetCommitStatus.Refused), Is.EqualTo(1));
        await this.AssertStateAsync(email: "before", bless: 2, receipt: null).ConfigureAwait(false);
    }

    /// <summary>A pending delivery is taken in parts; taking more than is left is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task APendingDeliveryIsTakenInPartsAsync()
    {
        using var context = this._provider.CreateNewPlayerContext(this._configuration);
        var accountId = (await context.GetAccountByLoginNameAsync(Login).ConfigureAwait(false))!.GetId();
        var queued = Guid.NewGuid();
        Assert.That(await context.SaveChangesWithAssetOperationAsync(new AssetOperationRequest(queued, accountId, "jewel-web-withdraw", "q", "website", null, [], "bless", 5)).ConfigureAwait(false), Is.EqualTo(AssetCommitStatus.Committed));

        AssetOperationRequest Deliver(int count) => new(Guid.NewGuid(), accountId, "jewel-delivery", $"d{count}", "game", null, [], DeliveredOperationId: queued, DeliveredCount: count);
        Assert.That(await context.SaveChangesWithAssetOperationAsync(Deliver(3)).ConfigureAwait(false), Is.EqualTo(AssetCommitStatus.Committed));
        Assert.That(await context.SaveChangesWithAssetOperationAsync(Deliver(3)).ConfigureAwait(false), Is.EqualTo(AssetCommitStatus.Refused));
        Assert.That((await context.GetAssetOperationAsync(queued).ConfigureAwait(false))!.PendingCount, Is.EqualTo(2));
        Assert.That(await context.GetPendingAssetOperationsAsync(accountId).ConfigureAwait(false), Has.Count.EqualTo(1));
    }

    private static AssetOperationRequest Deposit(Guid operation, Guid accountId, int count, int capacity = 100)
        => new(operation, accountId, "jewel-deposit", $"bless:{count}", "game", null, [new JewelBankDelta("bless", count, capacity)]);

    private async Task AssertStateAsync(string email, int bless, Guid? receipt)
    {
        // A new context: what the database holds.
        using var context = this._provider.CreateNewPlayerContext(this._configuration);
        var account = (await context.GetAccountByLoginNameAsync(Login).ConfigureAwait(false))!;
        var balances = await context.GetJewelBankBalancesAsync(account.GetId()).ConfigureAwait(false);
        Assert.Multiple(async () =>
        {
            Assert.That(account.EMail, Is.EqualTo(email));
            Assert.That(balances.GetValueOrDefault("bless"), Is.EqualTo(bless));
            if (receipt is { } id)
            {
                Assert.That(await context.GetAssetOperationAsync(id).ConfigureAwait(false), Is.Not.Null);
            }
        });
    }
}
