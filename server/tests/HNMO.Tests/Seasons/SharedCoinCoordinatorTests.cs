// <copyright file="SharedCoinCoordinatorTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests.Seasons;

using System.Collections.Concurrent;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Seasons;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using Moq;

/// <summary>
/// Unit tests for <see cref="SharedCoinCoordinator"/> and <see cref="SharedCoinGid"/>:
/// two-phase canonical PREPARE + local <see cref="AssetOperation"/> decision in the entitlement transaction,
/// full fee refusal, locked realm recovery, and keeping prepared transactions pending when the local DB is unreadable.
/// </summary>
[TestFixture]
public class SharedCoinCoordinatorTests
{
    private InMemoryPersistenceContextProvider _provider = null!;
    private GameConfiguration _configuration = null!;

    /// <summary>Sets up an in-memory secondary database context.</summary>
    [SetUp]
    public async Task SetUpAsync()
    {
        SeasonSharedAccounts.Reset();
        this._provider = new InMemoryPersistenceContextProvider();
        using var setup = this._provider.CreateNewContext();
        this._configuration = setup.CreateNew<GameConfiguration>();
        await setup.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>Resets shared baseline cache.</summary>
    [TearDown]
    public void TearDown() => SeasonSharedAccounts.Reset();

    /// <summary>GID encoding/decoding embeds realm and operation id cleanly.</summary>
    [Test]
    public void GidRoundTripsRealmAndOperationId()
    {
        var opId = Guid.NewGuid();
        var gid = SharedCoinGid.Build("s9-world-2", opId);
        Assert.That(SharedCoinGid.IsValid(gid), Is.True);
        Assert.That(SharedCoinGid.TryParse(gid, out var parsedRealm, out var parsedOp), Is.True);
        Assert.That(parsedRealm, Is.EqualTo("s9-world-2"));
        Assert.That(parsedOp, Is.EqualTo(opId));
        Assert.That(SharedCoinGid.IsValid("hnmo:s9;DROP TABLE"), Is.False);
    }

    /// <summary>Happy path commits canonical PREPARE, local entitlement + AssetOperation receipt, and COMMIT PREPARED.</summary>
    [Test]
    public async Task ExecuteCommitsCanonicalAndLocalTogetherAsync()
    {
        var canonical = new FakeCoordinatedStore(new CoinBalances(100, 50, 10));
        using var local = this._provider.CreateNewPlayerContext(this._configuration);
        var account = CreateLocalAccount(local, "alice", 100);
        await local.SaveChangesAsync().ConfigureAwait(false);

        var opId = Guid.NewGuid();
        var result = await SharedCoinCoordinator.ExecuteAsync(
            canonical,
            local,
            () => this._provider.CreateNewPlayerContext(this._configuration),
            account,
            opId,
            new CoinBalances(-40, 0, 0),
            "shared-coin-spend",
            "game",
            "hash-40",
            "s9",
            beforeLocalSave: (_, _) =>
            {
                var item = local.CreateNew<CashShopStorageItem>();
                item.AccountId = account.GetId();
                item.ProductSequence = 32;
                item.PriceSequence = 54;
                return ValueTask.CompletedTask;
            }).ConfigureAwait(false);

        Assert.Multiple(async () =>
        {
            Assert.That(result.Outcome, Is.EqualTo(SharedCoinCommitOutcome.Committed));
            Assert.That(canonical.CommittedBalances.WCoinC, Is.EqualTo(60));
            Assert.That(account.WCoinC, Is.EqualTo(60));
            Assert.That(canonical.PreparedGids, Is.Empty);
            Assert.That(await local.GetAssetOperationAsync(opId).ConfigureAwait(false), Is.Not.Null);
            Assert.That((await local.GetCashShopStorageItemsAsync(account.GetId()).ConfigureAwait(false)).Count, Is.EqualTo(1));
        });
    }

    /// <summary>A fee that the canonical shared balance cannot cover is refused in full without partial debit or local write.</summary>
    [Test]
    public async Task InsufficientSharedBalanceIsRefusedInFullAsync()
    {
        var canonical = new FakeCoordinatedStore(new CoinBalances(30, 0, 0));
        using var local = this._provider.CreateNewPlayerContext(this._configuration);
        var account = CreateLocalAccount(local, "alice", 100);
        await local.SaveChangesAsync().ConfigureAwait(false);

        var opId = Guid.NewGuid();
        var result = await SharedCoinCoordinator.ExecuteAsync(
            canonical,
            local,
            () => this._provider.CreateNewPlayerContext(this._configuration),
            account,
            opId,
            new CoinBalances(-40, 0, 0),
            "shared-coin-spend",
            "game",
            "hash-40",
            "s9").ConfigureAwait(false);

        Assert.Multiple(async () =>
        {
            Assert.That(result.Outcome, Is.EqualTo(SharedCoinCommitOutcome.Refused));
            Assert.That(canonical.CommittedBalances.WCoinC, Is.EqualTo(30), "zero coins taken on full refusal");
            Assert.That(canonical.PreparedGids, Is.Empty);
            Assert.That(await local.GetAssetOperationAsync(opId).ConfigureAwait(false), Is.Null);
        });
    }

    /// <summary>When local save fails, canonical prepared transaction is rolled back immediately.</summary>
    [Test]
    public async Task LocalSaveFailureRollsBackPreparedTransactionAsync()
    {
        var canonical = new FakeCoordinatedStore(new CoinBalances(100, 0, 0));
        var failingLocal = new Mock<IPlayerContext>();
        var account = new HNMO.Persistence.BasicModel.Account { Id = Guid.NewGuid(), LoginName = "alice", WCoinC = 100 };
        failingLocal.Setup(c => c.SaveChangesWithAssetOperationAsync(It.IsAny<AssetOperationRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("local crash"));
        using var checkLocal = this._provider.CreateNewPlayerContext(this._configuration);

        var opId = Guid.NewGuid();
        var result = await SharedCoinCoordinator.ExecuteAsync(
            canonical,
            failingLocal.Object,
            () => checkLocal,
            account,
            opId,
            new CoinBalances(-40, 0, 0),
            "shared-coin-spend",
            "game",
            "hash-40",
            "s9").ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(SharedCoinCommitOutcome.Refused));
            Assert.That(canonical.CommittedBalances.WCoinC, Is.EqualTo(100));
            Assert.That(canonical.PreparedGids, Is.Empty);
            Assert.That(account.WCoinC, Is.EqualTo(100));
        });
    }

    /// <summary>
    /// Locked recovery resolves prepared transactions for its own realm only, commits those with a local receipt,
    /// rolls back those without a receipt, skips active worker locks, and keeps them pending while the local DB cannot be read.
    /// </summary>
    [Test]
    public async Task RecoveryResolvesOwnRealmKeepsPendingWhenUnreadableAndRespectsLocksAsync()
    {
        var canonical = new FakeCoordinatedStore(new CoinBalances(100, 0, 0));
        using var local = this._provider.CreateNewPlayerContext(this._configuration);
        var account = CreateLocalAccount(local, "alice", 100);
        await local.SaveChangesAsync().ConfigureAwait(false);

        var committedOp = Guid.NewGuid();
        await local.SaveChangesWithAssetOperationAsync(new AssetOperationRequest(committedOp, account.GetId(), "shared-coin-spend", "h1", "game", null, [])).ConfigureAwait(false);
        var orphanOp = Guid.NewGuid();
        var activeOp = Guid.NewGuid();
        var otherRealmOp = Guid.NewGuid();

        var gidCommit = SharedCoinGid.Build("s9", committedOp);
        var gidOrphan = SharedCoinGid.Build("s9", orphanOp);
        var gidActive = SharedCoinGid.Build("s9", activeOp);
        var gidOther = SharedCoinGid.Build("arena", otherRealmOp);

        await canonical.SeedPreparedAsync(gidCommit, new CoinBalances(-20, 0, 0));
        await canonical.SeedPreparedAsync(gidOrphan, new CoinBalances(-30, 0, 0));
        await canonical.SeedPreparedAsync(gidActive, new CoinBalances(-10, 0, 0));
        await canonical.SeedPreparedAsync(gidOther, new CoinBalances(-5, 0, 0));
        canonical.ActiveLocks.Add(SharedCoinGid.OperationLockKey(gidActive));

        // 1. Local database unreachable: keep everything pending!
        var pendingReport = await SharedCoinCoordinator.RecoverAsync(
            canonical,
            () => throw new TimeoutException("local DB down"),
            "s9").ConfigureAwait(false);
        Assert.Multiple(() =>
        {
            Assert.That(pendingReport.LockAcquired, Is.True);
            Assert.That(pendingReport.Committed, Is.Empty);
            Assert.That(pendingReport.RolledBack, Is.Empty);
            Assert.That(pendingReport.KeptPendingLocalUnreadable, Is.EquivalentTo(new[] { gidCommit, gidOrphan }));
            Assert.That(pendingReport.SkippedActive, Is.EquivalentTo(new[] { gidActive }));
            Assert.That(canonical.CommittedBalances.WCoinC, Is.EqualTo(100));
        });

        // 2. Concurrent recovery pass when lock is already held: skipped cleanly.
        canonical.RecoveryLockHeldExternally = true;
        var contested = await SharedCoinCoordinator.RecoverAsync(
            canonical,
            () => this._provider.CreateNewPlayerContext(this._configuration),
            "s9").ConfigureAwait(false);
        Assert.That(contested.LockAcquired, Is.False);
        canonical.RecoveryLockHeldExternally = false;

        // 3. Local database reachable: commits committedOp, rolls back orphanOp, leaves activeOp & arena realm intact.
        var resolved = await SharedCoinCoordinator.RecoverAsync(
            canonical,
            () => this._provider.CreateNewPlayerContext(this._configuration),
            "s9").ConfigureAwait(false);
        Assert.Multiple(() =>
        {
            Assert.That(resolved.LockAcquired, Is.True);
            Assert.That(resolved.Committed, Is.EqualTo(new[] { gidCommit }));
            Assert.That(resolved.RolledBack, Is.EqualTo(new[] { gidOrphan }));
            Assert.That(resolved.SkippedActive, Is.EqualTo(new[] { gidActive }));
            Assert.That(canonical.CommittedBalances.WCoinC, Is.EqualTo(80));
            Assert.That(canonical.PreparedGids, Does.Contain(gidActive));
            Assert.That(canonical.PreparedGids, Does.Contain(gidOther));
        });
    }

    private static Account CreateLocalAccount(IPlayerContext context, string login, int wCoinC)
    {
        var acc = context.CreateNew<Account>();
        acc.LoginName = login;
        acc.PasswordHash = "x";
        acc.Vault = context.CreateNew<ItemStorage>();
        acc.WCoinC = wCoinC;
        return acc;
    }

    private sealed class FakeCoordinatedStore(CoinBalances initial) : ICoordinatedSharedCoinStore
    {
        private readonly ConcurrentDictionary<string, CoinBalances> _prepared = new(StringComparer.Ordinal);

        public CoinBalances CommittedBalances { get; private set; } = initial;

        public IReadOnlyCollection<string> PreparedGids => this._prepared.Keys.ToList();

        public HashSet<long> ActiveLocks { get; } = [];

        public bool RecoveryLockHeldExternally { get; set; }

        public ValueTask SeedPreparedAsync(string gid, CoinBalances delta)
        {
            this._prepared[gid] = delta;
            return ValueTask.CompletedTask;
        }

        public ValueTask<SharedAccount?> FindAsync(string loginName, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<SharedAccount?>(new SharedAccount(loginName, "x", string.Empty, string.Empty, AccountState.Normal, null, null, this.CommittedBalances));

        public ValueTask<CoinBalances?> AddCoinsAsync(string loginName, CoinBalances change, CancellationToken cancellationToken = default)
        {
            this.CommittedBalances = new CoinBalances(
                Math.Max(0, this.CommittedBalances.WCoinC + change.WCoinC),
                Math.Max(0, this.CommittedBalances.WCoinP + change.WCoinP),
                Math.Max(0, this.CommittedBalances.GoblinPoints + change.GoblinPoints));
            return ValueTask.FromResult<CoinBalances?>(this.CommittedBalances);
        }

        public ValueTask<PreparedCoinTransaction?> BeginPreparedCoinChangeAsync(string loginName, CoinBalances change, string gid, long operationLockKey, CancellationToken cancellationToken = default)
        {
            var next = new CoinBalances(
                this.CommittedBalances.WCoinC + change.WCoinC,
                this.CommittedBalances.WCoinP + change.WCoinP,
                this.CommittedBalances.GoblinPoints + change.GoblinPoints);
            if (next.WCoinC < 0 || next.WCoinP < 0 || next.GoblinPoints < 0)
            {
                return ValueTask.FromResult<PreparedCoinTransaction?>(null);
            }

            this._prepared[gid] = change;
            this.ActiveLocks.Add(operationLockKey);
            return ValueTask.FromResult<PreparedCoinTransaction?>(
                new PreparedCoinTransaction(gid, this.CommittedBalances, next, new FakeLock(() => this.ActiveLocks.Remove(operationLockKey))));
        }

        public ValueTask CommitPreparedAsync(string gid, CancellationToken cancellationToken = default)
        {
            if (this._prepared.TryRemove(gid, out var delta))
            {
                this.CommittedBalances = new CoinBalances(
                    this.CommittedBalances.WCoinC + delta.WCoinC,
                    this.CommittedBalances.WCoinP + delta.WCoinP,
                    this.CommittedBalances.GoblinPoints + delta.GoblinPoints);
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask RollbackPreparedAsync(string gid, CancellationToken cancellationToken = default)
        {
            this._prepared.TryRemove(gid, out _);
            return ValueTask.CompletedTask;
        }

        public ValueTask<IReadOnlyList<string>> ListPreparedTransactionsAsync(string realmId, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<string>>(
                this._prepared.Keys.Where(g => SharedCoinGid.TryParse(g, out var r, out _) && r == realmId).ToList());

        public ValueTask<IAsyncDisposable?> TryAcquireRecoveryLockAsync(string realmId, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IAsyncDisposable?>(this.RecoveryLockHeldExternally ? null : new FakeLock(() => { }));

        public ValueTask<IAsyncDisposable?> TryLockOperationAsync(long operationLockKey, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IAsyncDisposable?>(this.ActiveLocks.Contains(operationLockKey) ? null : new FakeLock(() => { }));

        private sealed class FakeLock(Action release) : IAsyncDisposable
        {
            public ValueTask DisposeAsync()
            {
                release();
                return ValueTask.CompletedTask;
            }
        }
    }
}
