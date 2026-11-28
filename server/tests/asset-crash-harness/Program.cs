// <copyright file="Program.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

// The worker of the asset operation and two-phase shared coin crash tests: one command per process, so a SIGKILL
// ends it for real and the next command recovers/reads the databases with connections of its own.
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Seasons;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.EntityFramework;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

const string Login = "crashtest";
HNMO.PlugIns.JsonConverterRegistry.RegisterConverter(new HNMO.Persistence.EntityFramework.Json.BinaryAsHexJsonConverter());
// Every command reads the connection settings, some (coin-recover) before any context exists: set them up first, as
// the server's start does. Without it coin-recover stopped at "Call Initialize before." and never recovered anything.
if (!ConnectionConfigurator.IsInitialized)
{
    ConnectionConfigurator.Initialize(new ConfigFileDatabaseConnectionStringProvider());
}

var provider = new PersistenceContextProvider(new NullLoggerFactory(), null);
var command = args.Length > 0 ? args[0] : "state";

static string ResolveCanonicalConnectionString()
{
    if (Environment.GetEnvironmentVariable("HNMO_TEST_CANONICAL_CONN") is { Length: > 0 } explicitConn)
    {
        return explicitConn;
    }

    var localConn = PersistenceContextProvider.ResolveConnectionString()
        ?? throw new InvalidOperationException("No connection string configured.");
    var builder = new NpgsqlConnectionStringBuilder(localConn);
    builder.Database = HnmoSeasonInstance.SharedDatabaseName(builder.Database ?? "hnmo");
    return builder.ToString();
}

static string ResolveLocalOverrideConnectionString()
    => Environment.GetEnvironmentVariable("HNMO_TEST_LOCAL_OVERRIDE_CONN") ?? string.Empty;

if (command == "setup")
{
    using (await provider.ReCreateDatabaseAsync().ConfigureAwait(false))
    {
    }

    await new HNMO.Persistence.Initialization.VersionSeasonSix.DataInitialization(provider, new NullLoggerFactory()).CreateInitialDataAsync(1, true).ConfigureAwait(false);
    Console.WriteLine("setup done");
    return 0;
}

if (command == "coin-setup")
{
    // Initializes canonical database (s6) when run with default HNMO_SEASON, or secondary database (s9) when HNMO_SEASON=s9.
    using (await provider.ReCreateDatabaseAsync().ConfigureAwait(false))
    {
    }

    await new HNMO.Persistence.Initialization.VersionSeasonSix.DataInitialization(provider, new NullLoggerFactory()).CreateInitialDataAsync(1, true).ConfigureAwait(false);
    Console.WriteLine($"coin-setup {HnmoSeasonInstance.Current} done");
    return 0;
}

if (command == "coin-recover")
{
    var realm = args.Length > 1 ? args[1] : SharedCoinRealm.Current;
    var canonicalStore = new PostgresSharedAccountStore(ResolveCanonicalConnectionString());
    var localOverride = ResolveLocalOverrideConnectionString();
    Func<IPlayerContext> createLocal = string.IsNullOrEmpty(localOverride)
        ? () => provider.CreateNewPlayerContext(new HNMO.Persistence.BasicModel.GameConfiguration())
        : () => throw new InvalidOperationException($"Simulated unreachable local database ({localOverride}).");

    var report = await SharedCoinCoordinator.RecoverAsync(canonicalStore, createLocal, realm).ConfigureAwait(false);
    Console.WriteLine($"lock={report.LockAcquired} committed={report.Committed.Count} rolledBack={report.RolledBack.Count} keptPending={report.KeptPendingLocalUnreadable.Count} skippedActive={report.SkippedActive.Count}");
    return 0;
}

if (command == "coin-other-realm-prepare")
{
    var otherRealm = args[1];
    var opId = Guid.Parse(args[2]);
    var canonicalStore = new PostgresSharedAccountStore(ResolveCanonicalConnectionString());
    var gid = SharedCoinGid.Build(otherRealm, opId);
    var prepared = await canonicalStore.BeginPreparedCoinChangeAsync(Login, new CoinBalances(-10, 0, 0), gid, SharedCoinGid.OperationLockKey(gid)).ConfigureAwait(false);
    if (prepared is null)
    {
        Console.WriteLine("refused");
        return 1;
    }

    await prepared.DisposeAsync().ConfigureAwait(false);
    Console.WriteLine($"prepared {gid}");
    return 0;
}

if (command == "coin-other-realm-rollback")
{
    var gid = args[1];
    var canonicalStore = new PostgresSharedAccountStore(ResolveCanonicalConnectionString());
    await canonicalStore.RollbackPreparedAsync(gid).ConfigureAwait(false);
    Console.WriteLine($"rolled back {gid}");
    return 0;
}

var configuration = (GameConfiguration)await new GameConfigurationDataSource(new NullLogger<GameConfigurationDataSource>(), provider).GetOwnerAsync().ConfigureAwait(false);
var bless = configuration.Items.First(item => item.Group == 14 && item.Number == 13);
using var context = provider.CreateNewPlayerContext(configuration);

switch (command)
{
    case "coin-reset":
    {
        // Resets canonical balance to 100 WCoinC and local S9 account with 0 items and 100 mirrored WCoinC.
        var canonicalStore = new PostgresSharedAccountStore(ResolveCanonicalConnectionString());
        if (await canonicalStore.FindAsync(Login).ConfigureAwait(false) is not { } existingCanon)
        {
            await using var canonConn = new NpgsqlConnection(ResolveCanonicalConnectionString());
            await canonConn.OpenAsync().ConfigureAwait(false);
            await using var ins = canonConn.CreateCommand();
            ins.CommandText = """
                INSERT INTO data."Account" ("Id", "LoginName", "PasswordHash", "SecurityCode", "EMail", "RegistrationDate", "State", "TimeZone", "IsVaultExtended", "IsTemplate", "IsBot", "IsNetworkObservationActive", "VaultPassword", "WCoinC", "WCoinP", "GoblinPoints")
                VALUES (@id, @login, 'x', '', '', now(), 0, 0, false, false, false, false, '', 100, 0, 0);
                """;
            ins.Parameters.AddWithValue("id", GuidV7.NewGuid());
            ins.Parameters.AddWithValue("login", Login);
            await ins.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        else
        {
            var delta = new CoinBalances(100 - existingCanon.Coins.WCoinC, -existingCanon.Coins.WCoinP, -existingCanon.Coins.GoblinPoints);
            await canonicalStore.AddCoinsAsync(Login, delta).ConfigureAwait(false);
        }

        if (await context.GetAccountByLoginNameAsync(Login).ConfigureAwait(false) is { } oldLocal)
        {
            await context.DeleteAsync(oldLocal).ConfigureAwait(false);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        var localAcc = context.CreateNew<Account>();
        localAcc.LoginName = Login;
        localAcc.PasswordHash = "x";
        localAcc.Vault = context.CreateNew<ItemStorage>();
        localAcc.WCoinC = 100;
        await context.SaveChangesAsync().ConfigureAwait(false);
        Console.WriteLine("coin-reset done");
        return 0;
    }

    case "coin-spend":
    {
        var opId = Guid.Parse(args[1]);
        var spendAmount = args.Length > 2 ? int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) : 40;
        var realm = args.Length > 3 ? args[3] : SharedCoinRealm.Current;
        var localAcc = (await context.GetAccountByLoginNameAsync(Login).ConfigureAwait(false))!;
        var canonicalStore = new PostgresSharedAccountStore(ResolveCanonicalConnectionString());
        var payload = $"{realm}|{Login}|{opId:N}|-{spendAmount},0,0";
        var result = await SharedCoinCoordinator.ExecuteAsync(
            canonicalStore,
            context,
            () => provider.CreateNewPlayerContext(configuration),
            localAcc,
            opId,
            new CoinBalances(-spendAmount, 0, 0),
            "shared-coin-spend",
            "game",
            AssetOperationCommitter.PayloadHash(payload),
            realm,
            beforeLocalSave: (_, _) =>
            {
                var entitlement = context.CreateNew<CashShopStorageItem>();
                entitlement.AccountId = localAcc.GetId();
                entitlement.ProductSequence = 32;
                entitlement.PriceSequence = 54;
                entitlement.AddedAt = DateTime.UtcNow;
                return ValueTask.CompletedTask;
            }).ConfigureAwait(false);
        Console.WriteLine($"outcome={result.Outcome} afterC={result.After.WCoinC}");
        return 0;
    }

    case "coin-state":
    {
        var canonicalStore = new PostgresSharedAccountStore(ResolveCanonicalConnectionString());
        var shared = await canonicalStore.FindAsync(Login).ConfigureAwait(false);
        var prepared = await canonicalStore.ListPreparedTransactionsAsync(SharedCoinRealm.Current).ConfigureAwait(false);
        var localAcc = (await context.GetAccountByLoginNameAsync(Login).ConfigureAwait(false))!;
        var entitlements = (await context.GetCashShopStorageItemsAsync(localAcc.GetId()).ConfigureAwait(false)).Count;
        var receipts = 0;
        foreach (var id in args.Skip(1).Select(Guid.Parse))
        {
            receipts += await context.GetAssetOperationAsync(id).ConfigureAwait(false) is null ? 0 : 1;
        }

        Console.WriteLine($"canon={shared?.Coins.WCoinC ?? -1} local={localAcc.WCoinC} items={entitlements} receipts={receipts} prepared={prepared.Count}");
        return 0;
    }

    case "reset":
    {
        // A fresh account with 4 Jewels of Bless in its vault and nothing in the bank.
        if (await context.GetAccountByLoginNameAsync(Login).ConfigureAwait(false) is { } old)
        {
            await context.DeleteAsync(old).ConfigureAwait(false);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        var account = context.CreateNew<Account>();
        account.LoginName = Login;
        account.PasswordHash = "x";
        account.Vault = context.CreateNew<ItemStorage>();
        for (byte slot = 0; slot < 4; slot++)
        {
            var item = context.CreateNew<Item>();
            item.Definition = bless;
            item.Durability = 1;
            item.ItemSlot = slot;
            account.Vault.Items.Add(item);
        }

        await context.SaveChangesAsync().ConfigureAwait(false);
        Console.WriteLine("reset done");
        return 0;
    }

    case "deposit":
    case "withdraw":
    {
        var operationId = Guid.Parse(args[1]);
        var account = (await context.GetAccountByLoginNameAsync(Login).ConfigureAwait(false))!;
        var vault = account.Vault!;
        AssetOperationRequest request;
        if (command == "deposit")
        {
            // The 4 jewels leave the vault (on a retry they may be gone already) and the bank gets 4.
            foreach (var item in vault.Items.Where(i => i.Definition == bless).ToList())
            {
                vault.Items.Remove(item);
                await context.DeleteAsync(item).ConfigureAwait(false);
            }

            request = new AssetOperationRequest(operationId, account.GetId(), "jewel-deposit", "deposit-bless-4", "game", null, [new JewelBankDelta("bless", 4, 100)]);
        }
        else
        {
            // 3 jewels come into the vault and the bank gives 3.
            for (byte slot = 10; slot < 13; slot++)
            {
                var item = context.CreateNew<Item>();
                item.Definition = bless;
                item.Durability = 1;
                item.ItemSlot = slot;
                vault.Items.Add(item);
            }

            request = new AssetOperationRequest(operationId, account.GetId(), "jewel-withdraw", "withdraw-bless-3", "game", null, [new JewelBankDelta("bless", -3, int.MaxValue)]);
        }

        try
        {
            var status = await context.SaveChangesWithAssetOperationAsync(request).ConfigureAwait(false);
            Console.WriteLine($"status {status}");
        }
        catch (Exception ex)
        {
            // What a caller does with a lost answer: read the outcome with a connection of its own.
            using var check = provider.CreateNewPlayerContext(configuration);
            var receipt = await check.GetAssetOperationAsync(operationId).ConfigureAwait(false);
            Console.WriteLine($"error {ex.GetType().Name}; outcome {(receipt is null ? "not committed" : "committed")}");
        }

        return 0;
    }

    default:
    {
        var account = (await context.GetAccountByLoginNameAsync(Login).ConfigureAwait(false))!;
        var inVault = account.Vault!.Items.Count(i => i.Definition == bless);
        var bank = (await context.GetJewelBankBalancesAsync(account.GetId()).ConfigureAwait(false)).GetValueOrDefault("bless");
        var receipts = 0;
        foreach (var id in args.Skip(1).Select(Guid.Parse))
        {
            receipts += await context.GetAssetOperationAsync(id).ConfigureAwait(false) is null ? 0 : 1;
        }

        Console.WriteLine($"vault {inVault} bank {bank} receipts {receipts}");
        return 0;
    }
}
