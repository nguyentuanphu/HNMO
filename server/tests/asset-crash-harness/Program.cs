// <copyright file="Program.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

// The worker of the asset operation and two-phase shared coin crash tests: one command per process, so a SIGKILL
// ends it for real and the next command recovers/reads the databases with connections of its own.
using System.Globalization;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Offline;
using HNMO.GameLogic.PlayerActions.Items;
using HNMO.GameLogic.PlayerActions.Trade;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.SeasonNineSystems;
using HNMO.GameLogic.Seasons;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.Trade;
using Moq;
using HNMO.PlugIns;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.EntityFramework;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

const string Login = "crashtest";
const string Login2 = "crashtest2";
HNMO.PlugIns.JsonConverterRegistry.RegisterConverter(new HNMO.Persistence.EntityFramework.Json.BinaryAsHexJsonConverter());
// Every command reads the connection settings, some (coin-recover) before any context exists: set them up first, as
// the server's start does. Without it coin-recover stopped at "Call Initialize before." and never recovered anything.
if (!ConnectionConfigurator.IsInitialized)
{
    ConnectionConfigurator.Initialize(new ConfigFileDatabaseConnectionStringProvider());
}

var provider = new PersistenceContextProvider(new NullLoggerFactory(), null);
provider.EnableHnmoRowPersistence();
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

    case "trade-reset":
    {
        async Task<Guid> SetupTraderAsync(string login, string charName, int initialZen)
        {
            using var ctx = provider.CreateNewPlayerContext(configuration);
            if (await ctx.GetAccountByLoginNameAsync(login).ConfigureAwait(false) is { } old)
            {
                await ctx.DeleteAsync(old).ConfigureAwait(false);
                await ctx.SaveChangesAsync().ConfigureAwait(false);
            }

            var acc = ctx.CreateNew<Account>();
            acc.LoginName = login;
            acc.PasswordHash = "x";
            acc.Vault = ctx.CreateNew<ItemStorage>();

            var ch = ctx.CreateNew<Character>();
            ch.Name = charName;
            ch.CharacterClass = configuration.CharacterClasses.First();
            ch.CurrentMap = configuration.Maps.First(m => m.Number == 0);
            ch.Inventory = ctx.CreateNew<ItemStorage>();
            ch.Inventory.Money = initialZen;

            var item = ctx.CreateNew<Item>();
            item.Definition = bless;
            item.Durability = 1;
            item.ItemSlot = 12;
            ch.Inventory.Items.Add(item);

            acc.Characters.Add(ch);
            await ctx.SaveChangesAsync().ConfigureAwait(false);
            return item.GetId();
        }

        var idA = await SetupTraderAsync(Login, "CrashA", 1000).ConfigureAwait(false);
        var idB = await SetupTraderAsync(Login2, "CrashB", 500).ConfigureAwait(false);
        Console.WriteLine($"{idA:D} {idB:D}");
        return 0;
    }

    case "trade":
    {
        var opId = Guid.Parse(args[1]);
        var expectedIdA = args.Length > 2 ? Guid.Parse(args[2]) : Guid.Empty;
        var expectedIdB = args.Length > 3 ? Guid.Parse(args[3]) : Guid.Empty;

        var gameConfig = configuration;
        var mapInit = new MapInitializer(gameConfig, new NullLogger<MapInitializer>(), NullDropGenerator.Instance, null);
        var plugInMgr = new PlugInManager([], new NullLoggerFactory(), null, null);
        var gc = new GameContext(gameConfig, provider, mapInit, new NullLoggerFactory(), plugInMgr, NullDropGenerator.Instance, new ConfigurationChangeMediator());
        mapInit.PlugInManager = gc.PlugInManager;
        mapInit.PathFinderPool = gc.PathFinderPool;

        async Task<OfflinePlayer> LoadPlayerAsync(string login)
        {
            var p = new TradeHarnessPlayer(gc);
            var acc = (await p.PersistenceContext.GetAccountByLoginNameAsync(login).ConfigureAwait(false))!;
            p.Account = acc;
            var ch = acc.Characters.First();
            await p.PlayerState.TryAdvanceToAsync(PlayerState.LoginScreen).ConfigureAwait(false);
            await p.PlayerState.TryAdvanceToAsync(PlayerState.Authenticated).ConfigureAwait(false);
            await p.PlayerState.TryAdvanceToAsync(PlayerState.CharacterSelection).ConfigureAwait(false);
            await p.SetSelectedCharacterAsync(ch).ConfigureAwait(false);
            _ = await p.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
            return p;
        }

        var pA = await LoadPlayerAsync(Login).ConfigureAwait(false);
        var pB = await LoadPlayerAsync(Login2).ConfigureAwait(false);
        var itemA = pA.Inventory!.Items.FirstOrDefault(i => expectedIdA != Guid.Empty ? i.GetId() == expectedIdA : i.Definition == bless);
        var itemB = pB.Inventory!.Items.FirstOrDefault(i => expectedIdB != Guid.Empty ? i.GetId() == expectedIdB : i.Definition == bless);

        if (itemA is null || itemB is null)
        {
            // Post-commit retry check: replay the exact same OperationId via IAtomicTradeContext -> expects AlreadyCommitted.
            using var retryCtx = provider.CreateNewTradeContext();
            if (retryCtx is IAtomicTradeContext atomic)
            {
                var ids = new[] { expectedIdA, expectedIdB }.Select(g => g.ToString("N")).OrderBy(s => s);
                var payload = $"{pA.Inventory.ItemStorage.GetId():N}|{pB.Inventory!.ItemStorage.GetId():N}|300|100|{string.Join(",", ids)}";
                var req = new AssetOperationRequest(opId, pA.Account!.GetId(), "trade", AssetOperationCommitter.PayloadHash(payload), "game", null, []);
                var st = await atomic.SaveTradeWithAssetOperationAsync(req, [new(pA.Inventory.ItemStorage.GetId(), -200), new(pB.Inventory!.ItemStorage.GetId(), 200)]).ConfigureAwait(false);
                Console.WriteLine($"retry-status={st}");
            }

            return 0;
        }

        var reqOk = await new TradeRequestAction().RequestTradeAsync(pA, pB).ConfigureAwait(false);
        await new TradeAcceptAction().HandleTradeAcceptAsync(pB, true).ConfigureAwait(false);
        var mover = new MoveItemAction();
        await mover.MoveItemAsync(pA, itemA.ItemSlot, Storages.Inventory, 0, Storages.Trade).ConfigureAwait(false);
        await mover.MoveItemAsync(pB, itemB.ItemSlot, Storages.Inventory, 0, Storages.Trade).ConfigureAwait(false);
        var moneyAction = new TradeMoneyAction();
        await moneyAction.TradeMoneyAsync(pA, 300).ConfigureAwait(false);
        await moneyAction.TradeMoneyAsync(pB, 100).ConfigureAwait(false);
        var btn = new TradeButtonAction();
        await btn.TradeButtonChangedAsync(pA, TradeButtonState.Checked, opId).ConfigureAwait(false);
        try
        {
            await btn.TradeButtonChangedAsync(pB, TradeButtonState.Checked, opId).ConfigureAwait(false);
            Console.WriteLine("trade done");
        }
        catch (Exception ex)
        {
            using var check = provider.CreateNewPlayerContext(configuration);
            var receipt = await check.GetAssetOperationAsync(opId).ConfigureAwait(false);
            Console.WriteLine($"error {ex.GetType().Name}; outcome {(receipt is null ? "not committed" : "committed")}");
        }

        return 0;
    }

    case "trade-state":
    {
        var opId = Guid.Parse(args[1]);
        var idA = Guid.Parse(args[2]);
        var idB = Guid.Parse(args[3]);
        var accA = (await context.GetAccountByLoginNameAsync(Login).ConfigureAwait(false))!;
        var accB = (await context.GetAccountByLoginNameAsync(Login2).ConfigureAwait(false))!;
        var invA = accA.Characters.First().Inventory!;
        var invB = accB.Characters.First().Inventory!;

        var allA = invA.Items.Select(i => i.GetId()).ToHashSet();
        var allB = invB.Items.Select(i => i.GetId()).ToHashSet();
        var inA_ofA = allA.Contains(idA);
        var inB_ofA = allB.Contains(idA);
        var inA_ofB = allA.Contains(idB);
        var inB_ofB = allB.Contains(idB);
        var aLoc = inA_ofA && !inB_ofA ? "A" : !inA_ofA && inB_ofA ? "B" : "ERR";
        var bLoc = inA_ofB && !inB_ofB ? "A" : !inA_ofB && inB_ofB ? "B" : "ERR";
        var receipts = await context.GetAssetOperationAsync(opId).ConfigureAwait(false) is null ? 0 : 1;
        var sum = invA.Money + invB.Money;
        Console.WriteLine($"itemA={aLoc} itemB={bLoc} countA={invA.Items.Count} countB={invB.Items.Count} zenA={invA.Money} zenB={invB.Money} zenSum={sum} receipts={receipts}");
        return 0;
    }

    case "muun-reset":
    {
        if (await context.GetAccountByLoginNameAsync(Login).ConfigureAwait(false) is { } old)
        {
            await context.DeleteAsync(old).ConfigureAwait(false);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        var lukiDef = configuration.Items.First(i => i.Group == MuunCompanion.MuunGroup && i.Number == 212);
        var tonyDef = configuration.Items.First(i => i.Group == MuunCompanion.MuunGroup && i.Number == 214);
        var convDef = configuration.Items.First(i => i.Group == MuunCompanion.MuunGroup && i.Number == MuunCompanion.EnergyConverterNumber);

        var acc = context.CreateNew<Account>();
        acc.LoginName = Login;
        acc.PasswordHash = "x";
        acc.Vault = context.CreateNew<ItemStorage>();

        var ch = context.CreateNew<Character>();
        ch.Name = "CrashMuun";
        ch.CharacterClass = configuration.CharacterClasses.First();
        ch.CurrentMap = configuration.Maps.First(m => m.Number == 0);
        ch.Inventory = context.CreateNew<ItemStorage>();

        Item AddSlotItem(ItemDefinition def, byte slot, byte level)
        {
            var it = context.CreateNew<Item>();
            it.Definition = def;
            it.Level = level;
            it.Durability = 1;
            it.ItemSlot = slot;
            ch.Inventory.Items.Add(it);
            return it;
        }

        var luki = AddSlotItem(lukiDef, 12, 1);
        var tony = AddSlotItem(tonyDef, 13, 1);
        AddSlotItem(convDef, 14, 0);

        acc.Characters.Add(ch);
        await context.SaveChangesAsync().ConfigureAwait(false);

        var connStr = PersistenceContextProvider.ResolveConnectionString()!;
        var backend = new PostgresHnmoPlayerStateRowBackend(connStr);
        backend.EnsureSchema();
        var stateJson = JsonSerializer.Serialize(new MuunState { ActiveItemId = luki.GetId(), Energy = 10 }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        backend.UpsertBatch([new HnmoFeatureRow(MuunCompanion.FeatureKey, ch.GetId().ToString("N"), stateJson, DateTime.UtcNow)], []);
        Console.WriteLine($"{luki.GetId():D} {tony.GetId():D}");
        return 0;
    }

    case "muun-convert":
    case "muun-levelup":
    {
        var opId = Guid.Parse(args[1]);
        var s9Cfg = new PlugInConfiguration
        {
            TypeId = typeof(SeasonNineSystemsPlugIn).GUID,
            IsActive = true,
            CustomConfiguration = JsonSerializer.Serialize(new SeasonNineSystemsConfiguration { Enabled = true, MuunEnabled = true, MuunLevelUpEnergyCost = 5, MuunLevelUpEnergyCosts = null, MuunEvoPointMultiplier = 1 }),
        };
        var mapInit = new MapInitializer(configuration, new NullLogger<MapInitializer>(), NullDropGenerator.Instance, null);
        var plugInMgr = new PlugInManager([s9Cfg], new NullLoggerFactory(), null, null);
        var gc = new GameContext(configuration, provider, mapInit, new NullLoggerFactory(), plugInMgr, NullDropGenerator.Instance, new ConfigurationChangeMediator());
        mapInit.PlugInManager = gc.PlugInManager;
        mapInit.PathFinderPool = gc.PathFinderPool;

        var plugIn = SeasonNineSystemsPlugIn.Get(gc)!;
        plugIn.IsSeasonNineRealm = () => true;

        var player = new OfflinePlayer(gc);
        var acc = (await player.PersistenceContext.GetAccountByLoginNameAsync(Login).ConfigureAwait(false))!;
        player.Account = acc;
        var ch = acc.Characters.First();
        await player.PlayerState.TryAdvanceToAsync(PlayerState.LoginScreen).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.Authenticated).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.CharacterSelection).ConfigureAwait(false);
        await player.SetSelectedCharacterAsync(ch).ConfigureAwait(false);

        if (command == "muun-convert")
        {
            if (player.Inventory!.GetItem(13) is null)
            {
                // Post-commit retry: same OperationId on AlreadyCommitted receipt must return AlreadyCommitted and write nothing.
                var req = new AssetOperationRequest(opId, acc.GetId(), "muun-convert", AssetOperationCommitter.PayloadHash("retry"), "game", null, []);
                var st = await player.PersistenceContext.SaveChangesWithAssetOperationAsync(req).ConfigureAwait(false);
                Console.WriteLine($"retry-status={st}");
                return 0;
            }

            await plugIn.Muuns.ConvertAsync(player, 13, opId).ConfigureAwait(false);
            Console.WriteLine("muun-convert done");
        }
        else
        {
            await plugIn.Muuns.LevelUpAsync(player, opId).ConfigureAwait(false);
            Console.WriteLine("muun-levelup done");
        }

        return 0;
    }

    case "muun-state":
    {
        var opId = Guid.Parse(args[1]);
        var lukiId = Guid.Parse(args[2]);
        var acc = (await context.GetAccountByLoginNameAsync(Login).ConfigureAwait(false))!;
        var ch = acc.Characters.First();
        var items = ch.Inventory!.Items.ToList();
        var luki = items.FirstOrDefault(i => i.GetId() == lukiId);
        var tonys = items.Count(i => i.Definition?.Group == MuunCompanion.MuunGroup && i.Definition?.Number == 214);
        var convs = items.Count(i => i.Definition?.Group == MuunCompanion.MuunGroup && i.Definition?.Number == MuunCompanion.EnergyConverterNumber);
        var backend = new PostgresHnmoPlayerStateRowBackend(PersistenceContextProvider.ResolveConnectionString()!);
        var row = backend.FindRow(MuunCompanion.FeatureKey, ch.GetId().ToString("N"));
        var muunState = row is null ? new MuunState() : JsonSerializer.Deserialize<MuunState>(row.PayloadJson, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!;
        var receipts = await context.GetAssetOperationAsync(opId).ConfigureAwait(false) is null ? 0 : 1;
        Console.WriteLine($"lukiSame={(luki is not null).ToString().ToLowerInvariant()} lukiLv={(int)(luki?.Level ?? 0)} tonys={tonys} convs={convs} energy={muunState.Energy} receipts={receipts}");
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

/// <summary>An offline player whose view plug-ins are stubs, so the trade actions (which need the views of both traders) run for real.</summary>
internal sealed class TradeHarnessPlayer : OfflinePlayer
{
    public TradeHarnessPlayer(IGameContext gameContext)
        : base(gameContext)
    {
    }

    protected override ICustomPlugInContainer<IViewPlugIn> CreateViewPlugInContainer() => new StubViewPlugInContainer();
}

internal sealed class StubViewPlugInContainer : ICustomPlugInContainer<IViewPlugIn>
{
    private readonly Dictionary<Type, IViewPlugIn> _stubs = new();

    public T GetPlugIn<T>()
        where T : class, IViewPlugIn
    {
        if (!this._stubs.TryGetValue(typeof(T), out var stub))
        {
            stub = new Mock<T>().Object;
            this._stubs.Add(typeof(T), stub);
        }

        return (T)stub;
    }
}