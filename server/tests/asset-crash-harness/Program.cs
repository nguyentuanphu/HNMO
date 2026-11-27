// <copyright file="Program.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

// The worker of the asset operation crash test (run.sh): one command per process, so a SIGKILL ends it for real and the
// next command reads the database with a connection of its own.
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.Persistence;
using HNMO.Persistence.EntityFramework;
using Microsoft.Extensions.Logging.Abstractions;

const string Login = "crashtest";
HNMO.PlugIns.JsonConverterRegistry.RegisterConverter(new HNMO.Persistence.EntityFramework.Json.BinaryAsHexJsonConverter());
var provider = new PersistenceContextProvider(new NullLoggerFactory(), null);
var command = args.Length > 0 ? args[0] : "state";

if (command == "setup")
{
    using (await provider.ReCreateDatabaseAsync().ConfigureAwait(false))
    {
    }

    await new HNMO.Persistence.Initialization.VersionSeasonSix.DataInitialization(provider, new NullLoggerFactory()).CreateInitialDataAsync(1, true).ConfigureAwait(false);
    Console.WriteLine("setup done");
    return 0;
}

var configuration = (GameConfiguration)await new GameConfigurationDataSource(new NullLogger<GameConfigurationDataSource>(), provider).GetOwnerAsync().ConfigureAwait(false);
var bless = configuration.Items.First(item => item.Group == 14 && item.Number == 13);
using var context = provider.CreateNewPlayerContext(configuration);

switch (command)
{
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
