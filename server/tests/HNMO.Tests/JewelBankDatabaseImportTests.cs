// <copyright file="JewelBankDatabaseImportTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.JewelBank;
using HNMO.Persistence;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// <see cref="JewelBankDatabaseImport"/>: the balances and waiting withdrawals of the old JSON state file go into the
/// database once, whatever the number of runs, and a dry run writes nothing.
/// </summary>
[TestFixture]
public class JewelBankDatabaseImportTests
{
    private string _tempDir = string.Empty;

    private Guid _aliceId;

    /// <summary>Creates an isolated directory for the state file.</summary>
    [SetUp]
    public void SetUp()
    {
        this._tempDir = Path.Combine(Path.GetTempPath(), $"hnmo-jb-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this._tempDir);
    }

    /// <summary>Deletes the directory.</summary>
    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(this._tempDir))
        {
            Directory.Delete(this._tempDir, recursive: true);
        }
    }

    /// <summary>A dry run reports what would be imported, with totals and a checksum, and writes nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ADryRunWritesNothingAsync()
    {
        var (plugIn, player) = await this.CreateAsync().ConfigureAwait(false);
        var engine = plugIn.GetEngine();
        SeedJson(engine);

        var first = await JewelBankDatabaseImport.RunAsync(player.GameContext.PersistenceContextProvider, player.GameContext.Configuration, engine.Store, dryRun: true).ConfigureAwait(false);
        var second = await JewelBankDatabaseImport.RunAsync(player.GameContext.PersistenceContextProvider, player.GameContext.Configuration, engine.Store, dryRun: true).ConfigureAwait(false);

        Assert.Multiple(async () =>
        {
            Assert.That(first.WouldImport, Has.Count.EqualTo(3)); // bless, soul and the waiting withdrawal of alice
            Assert.That(first.JsonTotals["bless"], Is.EqualTo(7));
            Assert.That(first.JsonPending["soul"], Is.EqualTo(2));
            Assert.That(first.UnknownAccounts, Is.EqualTo(new[] { "ghost" }));
            Assert.That(second.InputChecksum, Is.EqualTo(first.InputChecksum));
            Assert.That(await player.PersistenceContext.GetJewelBankBalancesAsync(this._aliceId).ConfigureAwait(false), Is.Empty);
        });
    }

    /// <summary>
    /// The import puts the balances and the waiting withdrawals (as deliveries, not debited again) into the database;
    /// a second run, and a run after one stopped after the first account, change nothing more.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TheImportCountsEveryEntryOnceAsync()
    {
        var (plugIn, player) = await this.CreateAsync().ConfigureAwait(false);
        var engine = plugIn.GetEngine();
        SeedJson(engine);
        var provider = player.GameContext.PersistenceContextProvider;
        var configuration = player.GameContext.Configuration;

        // A run which stopped after the first account, then two full runs.
        await JewelBankDatabaseImport.RunAsync(provider, configuration, engine.Store, dryRun: false, "alice").ConfigureAwait(false);
        var full = await JewelBankDatabaseImport.RunAsync(provider, configuration, engine.Store, dryRun: false).ConfigureAwait(false);
        var again = await JewelBankDatabaseImport.RunAsync(provider, configuration, engine.Store, dryRun: false).ConfigureAwait(false);

        var accountId = this._aliceId;
        var balances = await player.PersistenceContext.GetJewelBankBalancesAsync(accountId).ConfigureAwait(false);
        var waiting = await player.PersistenceContext.GetPendingAssetOperationsAsync(accountId).ConfigureAwait(false);
        Assert.Multiple(() =>
        {
            Assert.That(full.Imported, Is.Empty, "alice was imported by the first run already");
            Assert.That(full.AlreadyImported, Is.EqualTo(3));
            Assert.That(again.Imported, Is.Empty);
            Assert.That(again.Conflicts, Is.Empty);
            Assert.That(balances["bless"], Is.EqualTo(7));
            Assert.That(balances["soul"], Is.EqualTo(1));
            Assert.That(waiting.Single().PendingCount, Is.EqualTo(2));
        });
    }

    /// <summary>A JSON entry changed after its import is reported as a conflict and not imported again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AChangedEntryIsAConflictAndNotImportedTwiceAsync()
    {
        var (plugIn, player) = await this.CreateAsync().ConfigureAwait(false);
        var engine = plugIn.GetEngine();
        SeedJson(engine);
        var provider = player.GameContext.PersistenceContextProvider;
        await JewelBankDatabaseImport.RunAsync(provider, player.GameContext.Configuration, engine.Store, dryRun: false).ConfigureAwait(false);
        Assert.That(engine.Deposit("alice", "bless", 3, "game", DateTime.UtcNow), Is.EqualTo(JewelBankResult.Ok));

        var report = await JewelBankDatabaseImport.RunAsync(provider, player.GameContext.Configuration, engine.Store, dryRun: false).ConfigureAwait(false);

        Assert.That(report.Conflicts, Has.Count.EqualTo(1));
        Assert.That((await player.PersistenceContext.GetJewelBankBalancesAsync(this._aliceId).ConfigureAwait(false))["bless"], Is.EqualTo(7));
    }

    /// <summary>The first Jewel Bank action of an account imports its JSON balance, so a withdrawal can use it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TheFirstActionImportsTheAccountsJsonBalanceAsync()
    {
        var (plugIn, player) = await this.CreateAsync().ConfigureAwait(false);
        SeedJson(plugIn.GetEngine());
        var bless = new BasicModel.ItemDefinition { Group = 14, Number = 13, Width = 1, Height = 1, Durability = 1 };
        player.GameContext.Configuration.Items.Add(bless);

        await plugIn.WithdrawAsync(player, "bless", 3).ConfigureAwait(false);

        Assert.That(player.Inventory!.Items.Where(i => i.Definition == bless).Sum(i => (int)i.Durability), Is.EqualTo(3));
        Assert.That((await player.PersistenceContext.GetJewelBankBalancesAsync(player.Account!.GetId()).ConfigureAwait(false))["bless"], Is.EqualTo(4));
    }

    private static void SeedJson(JewelBankEngine engine)
    {
        Assert.That(engine.Deposit("alice", "bless", 7, "game", DateTime.UtcNow), Is.EqualTo(JewelBankResult.Ok));
        Assert.That(engine.Deposit("alice", "soul", 3, "game", DateTime.UtcNow), Is.EqualTo(JewelBankResult.Ok));
        Assert.That(engine.TryWithdraw("alice", "soul", 2, "website", DateTime.UtcNow, enqueuePending: true), Is.EqualTo(JewelBankResult.Ok));
        Assert.That(engine.Deposit("ghost", "bless", 1, "game", DateTime.UtcNow), Is.EqualTo(JewelBankResult.Ok));
    }

    private async Task<(JewelBankPlugIn PlugIn, Player Player)> CreateAsync()
    {
        var context = GameContextTestHelper.CreateGameContext([]);
        var player = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        player.Account!.LoginName = "alice";

        // The account as the database has it, for the runs over all accounts.
        var stored = player.PersistenceContext.CreateNew<Account>();
        stored.LoginName = "alice";
        this._aliceId = stored.GetId();
        var plugIn = new JewelBankPlugIn
        {
            Configuration = new JewelBankConfiguration
            {
                FileName = Path.Combine(this._tempDir, "hnmo-state.json"),
                SafeZoneOnly = false,
                Jewels =
                [
                    new() { Key = "bless", NameEn = "Jewel of Bless", NameVi = "Ngọc Ước Nguyện", Group = 14, Number = 13, Enabled = true, MaxCapacity = 100 },
                    new() { Key = "soul", NameEn = "Jewel of Soul", NameVi = "Ngọc Tâm Linh", Group = 14, Number = 14, Enabled = true, MaxCapacity = 100 },
                ],
            },
        };
        return (plugIn, player);
    }
}
