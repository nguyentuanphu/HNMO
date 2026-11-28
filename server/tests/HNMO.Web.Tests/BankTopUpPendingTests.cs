// <copyright file="BankTopUpPendingTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.Web.AdminPanel.Auth;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.AdminPanel.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// Players' top-ups by bank transfer with a transfer code: any bank, the settings of the first version, waiting
/// top-ups, and coins added once whoever confirms (the host's Received button, Casso, SePay).
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class BankTopUpPendingTests
{
    private const string Ticket = "0123456789abcdef0123456789abcdef";
    private DirectoryInfo _dir = null!;
    private BankTopUpPendingStore _store = null!;
    private InMemoryPersistenceContextProvider _database = null!;
    private CashShopCoinService _wallet = null!;
    private BankTopUpService _service = null!;
    private BankTopUpConfiguration _config = null!;

    /// <summary>A fresh state file, database with the accounts "buyer" and "other", and one Vietcombank account.</summary>
    /// <returns>The task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._dir = Directory.CreateTempSubdirectory();
        this._store = BankTopUpPendingStore.Open(Path.Combine(this._dir.FullName, HnmoStateStore.StateFileName));
        this._database = new InMemoryPersistenceContextProvider();
        var game = new GameConfiguration();
        using (var context = this._database.CreateNewPlayerContext(game))
        {
            foreach (var login in new[] { "buyer", "other" })
            {
                context.CreateNew<Account>().LoginName = login;
            }

            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        var source = new Mock<IDataSource<GameConfiguration>>();
        source.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(game);
        this._wallet = new CashShopCoinService(this._database, source.Object);
        this._service = new BankTopUpService(this._wallet, NullLogger<BankTopUpService>.Instance, this._store);
        this._config = new BankTopUpConfiguration
        {
            AccountName = "NGUYEN VAN A",
            Accounts = [new BankTopUpAccount { Bin = "970436", Number = "0123456789" }, new BankTopUpAccount { Bin = "970422", Number = "999", Active = false }],
            VndPerCoin = 100,
            MinimumVnd = 10_000,
            TransferPrefix = "HNMO",
        };
    }

    /// <summary>Removes the state file.</summary>
    [TearDown]
    public void TearDown() => this._dir.Delete(recursive: true);

    /// <summary>Settings saved before the account list still offer their Techcombank and Vietcombank accounts.</summary>
    [Test]
    public void SettingsOfTheFirstVersionStillWork()
    {
        var old = JsonSerializer.Deserialize<BankTopUpConfiguration>("""{"AccountName":"NGUYEN VAN A","TechcombankAccount":" 1903 ","VietcombankAccount":"0071","VndPerCoin":50}""")!;
        Assert.That(old.UsableAccounts().Select(a => (a.Bin, a.Number)), Is.EqualTo(new[] { ("970407", "1903"), ("970436", "0071") }));
        old.MigrateLegacyAccounts();
        Assert.That(old.Accounts, Has.Count.EqualTo(2));
        Assert.That(old.TechcombankAccount + old.VietcombankAccount, Is.Empty);
        var again = JsonSerializer.Deserialize<BankTopUpConfiguration>(JsonSerializer.Serialize(old))!;
        Assert.That(again.UsableAccounts().Select(a => a.Number), Is.EqualTo(new[] { "1903", "0071" }));
        Assert.That(this._config.UsableAccounts().Select(a => a.Bin), Is.EqualTo(new[] { "970436" }), "a hidden account isn't offered");
        Assert.That(VietnameseBanks.All.Select(b => b.Bin).Distinct().Count(), Is.EqualTo(VietnameseBanks.All.Count));
    }

    /// <summary>The same choice gives the same code; 3 waiting per account; a code expires after 24 hours.</summary>
    [Test]
    public void WaitingTopUpsAreLimitedAndExpire()
    {
        var account = this._config.Accounts[0];
        var now = new DateTime(2026, 11, 28, 1, 0, 0, DateTimeKind.Utc);
        var (first, _) = this._store.Create("buyer", 50_000, 500, account, "HNMO", Ticket, now);
        var (same, _) = this._store.Create("BUYER", 50_000, 500, account, "HNMO", "ffffffffffffffffffffffffffffffff", now.AddMinutes(1));
        Assert.That(same!.Code, Is.EqualTo(first!.Code));
        Assert.That(first.Code, Does.Match("^HNMO[A-HJ-NP-Z2-9]{6}$"));
        Assert.That(this._store.Create("buyer", 60_000, 600, account, "HNMO", Ticket, now).TopUp, Is.Not.Null);
        Assert.That(this._store.Create("buyer", 70_000, 700, account, "HNMO", Ticket, now).TopUp, Is.Not.Null);
        Assert.That(this._store.Create("buyer", 80_000, 800, account, "HNMO", Ticket, now).Refusal, Is.EqualTo(BankTopUpPendingStore.CreateRefusal.TooManyForAccount));
        Assert.That(this._store.Create("other", 80_000, 800, account, "HNMO", Ticket, now).TopUp, Is.Not.Null);

        var later = now + BankTopUpPendingStore.Lifetime;
        Assert.That(this._store.Find(first.Id)!.IsOpen(later), Is.False);
        Assert.That(this._store.Create("buyer", 80_000, 800, account, "HNMO", Ticket, later).TopUp, Is.Not.Null, "expired ones don't count");
        Assert.That(this._store.ForTickets([Ticket]).Count, Is.EqualTo(5));
        Assert.That(this._store.ForTickets(["ffffffffffffffffffffffffffffffff"]).Single().Code, Is.EqualTo(first.Code));
        Assert.That(this._store.ForTickets(["short"]), Is.Empty);
        Assert.That(File.ReadAllText(Path.Combine(this._dir.FullName, HnmoStateStore.StateFileName)), Does.Not.Contain(Ticket), "only the ticket's hash is kept");
        Assert.That(this._store.All().Select(p => p.Code).Distinct().Count(), Is.EqualTo(this._store.All().Count));
    }

    /// <summary>The page refuses unknown accounts, amounts outside the limits and accounts which aren't the host's.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task WrongAccountAmountOrBankAccountIsRefusedAsync()
    {
        Assert.That((await this._service.CreatePendingAsync("nobody", 50_000, "970436", "0123456789", this._config, Ticket).ConfigureAwait(false)).Result, Is.EqualTo(BankTopUpCreateResult.AccountNotFound));
        Assert.That((await this._service.CreatePendingAsync("buyer", 9_000, "970436", "0123456789", this._config, Ticket).ConfigureAwait(false)).Result, Is.EqualTo(BankTopUpCreateResult.BadAmount));
        Assert.That((await this._service.CreatePendingAsync("buyer", BankTopUpService.MaximumVnd + 1, "970436", "0123456789", this._config, Ticket).ConfigureAwait(false)).Result, Is.EqualTo(BankTopUpCreateResult.BadAmount));
        Assert.That((await this._service.CreatePendingAsync("buyer", 50_000, "970422", "999", this._config, Ticket).ConfigureAwait(false)).Result, Is.EqualTo(BankTopUpCreateResult.UnknownBankAccount));
        Assert.That((await this._service.CreatePendingAsync("buyer", 50_000, "970436", "0123456789", null, Ticket).ConfigureAwait(false)).Result, Is.EqualTo(BankTopUpCreateResult.Off));
        var (ok, topUp) = await this._service.CreatePendingAsync("BUYER", 50_000, "970436", "0123456789", this._config, Ticket).ConfigureAwait(false);
        Assert.That(ok, Is.EqualTo(BankTopUpCreateResult.Created));
        Assert.That(topUp!.Login, Is.EqualTo("buyer"));
        Assert.That(topUp.Coins, Is.EqualTo(500));
    }

    /// <summary>Two clicks on Received at once and a retry add the coins once.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ReceivedCreditsOnceUnderADoubleClickAndARetryAsync()
    {
        var (_, topUp) = await this._service.CreatePendingAsync("buyer", 50_000, "970436", "0123456789", this._config, Ticket).ConfigureAwait(false);
        var clicks = await Task.WhenAll(
            this._service.ConfirmPendingAsync(topUp!.Id, this._config, "admin").AsTask(),
            this._service.ConfirmPendingAsync(topUp.Id, this._config, "admin").AsTask()).ConfigureAwait(false);
        Assert.That(clicks, Is.EquivalentTo(new[] { BankTopUpResult.Credited, BankTopUpResult.AlreadyCredited }));
        Assert.That(await this._service.ConfirmPendingAsync(topUp.Id, this._config, "admin").ConfigureAwait(false), Is.EqualTo(BankTopUpResult.AlreadyCredited));
        var grants = (await this._wallet.GetSummaryAsync("buyer").ConfigureAwait(false))!.Grants;
        Assert.That(grants.Select(g => g.Amount), Is.EqualTo(new[] { 500 }));
        Assert.That(this._store.Find(topUp.Id)!.Status, Is.EqualTo(BankTopUpPendingStatus.Credited));
        Assert.That(this._store.Find(topUp.Id)!.DoneBy, Is.EqualTo("admin"));
        Assert.That(this._service.RejectPending(topUp.Id, "admin"), Is.EqualTo(BankTopUpResult.AlreadyCredited), "credited stays credited");
        Assert.That(await this._service.ConfirmPendingAsync("missing", this._config, "admin").ConfigureAwait(false), Is.EqualTo(BankTopUpResult.NotFound));
    }

    /// <summary>Casso, SePay and the host's button on the same transfer add the coins once; a short transfer adds none.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ServicesAndTheButtonOnOneTransferCreditOnceAsync()
    {
        var (_, topUp) = await this._service.CreatePendingAsync("buyer", 50_000, "970436", "0123456789", this._config, Ticket).ConfigureAwait(false);
        var message = $"MBVCB.3278.{topUp!.Code.ToLowerInvariant()}.CT tu 0123 NGUYEN VAN B";
        Assert.That(await this._service.CreditAsync(new BankTransfer("casso", "1", "FT1", 40_000, message), this._config).ConfigureAwait(false), Is.EqualTo(BankTopUpResult.BelowRequested));
        Assert.That((await this._wallet.GetSummaryAsync("buyer").ConfigureAwait(false))!.Grants, Is.Empty, "less than asked: the host decides");

        var results = await Task.WhenAll(
            this._service.CreditAsync(new BankTransfer("casso", "2", "FT2", 50_000, message), this._config).AsTask(),
            this._service.CreditAsync(new BankTransfer("sepay", "9", "FT2", 50_000, $"HNMO {topUp.Code[4..]}"), this._config).AsTask(),
            this._service.ConfirmPendingAsync(topUp.Id, this._config, "admin").AsTask()).ConfigureAwait(false);
        Assert.That(results.Count(r => r == BankTopUpResult.Credited), Is.EqualTo(1));
        Assert.That(results.Count(r => r == BankTopUpResult.AlreadyCredited), Is.EqualTo(2));
        Assert.That((await this._wallet.GetSummaryAsync("buyer").ConfigureAwait(false))!.Grants, Has.Count.EqualTo(1));

        // A transfer without a code still works the old way ("HNMO login").
        Assert.That(await this._service.CreditAsync(new BankTransfer("casso", "3", "FT3", 20_000, "HNMO other"), this._config).ConfigureAwait(false), Is.EqualTo(BankTopUpResult.Credited));
    }

    /// <summary>A rejected top-up whose money arrives later is still credited.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ALateTransferCreditsARejectedTopUpAsync()
    {
        var (_, topUp) = await this._service.CreatePendingAsync("buyer", 50_000, "970436", "0123456789", this._config, Ticket).ConfigureAwait(false);
        Assert.That(this._service.RejectPending(topUp!.Id, "admin"), Is.EqualTo(BankTopUpResult.Rejected));
        Assert.That(await this._service.CreditAsync(new BankTransfer("sepay", "5", "FT5", 50_000, topUp.Code), this._config).ConfigureAwait(false), Is.EqualTo(BankTopUpResult.Credited));
        Assert.That(this._store.Find(topUp.Id)!.Status, Is.EqualTo(BankTopUpPendingStatus.Credited));
    }

    /// <summary>The admin panel's QR preview is the same VietQR text as the web game's top-up page (CRC included).</summary>
    /// <param name="bin">The bank id.</param>
    /// <param name="account">The account number.</param>
    /// <param name="holder">The holder.</param>
    /// <param name="amount">The amount.</param>
    /// <param name="message">The message.</param>
    /// <param name="expected">The payload the web page's vietqr.ts makes.</param>
    [TestCase("970436", "0123456789", "NGUYEN VAN A", 50000, "HNMO7K3QX2", "00020101021238540010A00000072701240006970436011001234567890208QRIBFTTA53037045405500005802VN5912NGUYEN VAN A62140810HNMO7K3QX26304839A")]
    [TestCase("970407", "19033334444", "Trần Thị Bé", 200000, "HNMOABC234", "00020101021238550010A000000727012500069704070111190333344440208QRIBFTTA530370454062000005802VN5911Tran Thi Be62140810HNMOABC23463040221")]
    [TestCase("970422", "0909123456", "", 0, "", "00020101021138540010A00000072701240006970422011009091234560208QRIBFTTA53037045802VN6304BB5E")]
    [TestCase("963388", "9021000123", "LÊ ĐỨC", 1000000, "HNMOZZ9988", "00020101021238540010A00000072701240006963388011090210001230208QRIBFTTA5303704540710000005802VN5906LE DUC62140810HNMOZZ998863044170")]
    public void PreviewMatchesTheTopUpPage(string bin, string account, string holder, long amount, string message, string expected)
    {
        Assert.That(BankTopUps.VietQrPayload(bin, account, holder, amount, message), Is.EqualTo(expected));
    }

    /// <summary>The page needs the cash shop policy; saving the settings checks the Administrator policy again.</summary>
    [Test]
    public void ThePageNeedsTheCashShopPolicy()
    {
        Assert.That(typeof(BankTopUps).GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy), Does.Contain(AdminPolicies.CashShop));
    }
}
