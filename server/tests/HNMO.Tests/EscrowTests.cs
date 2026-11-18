// <copyright file="EscrowTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HNMO.GameLogic.PlugIns.Escrow;

/// <summary>
/// Unit and concurrency tests for <see cref="EscrowEngine"/> and <see cref="EscrowStore"/> (ROADMAP 16.1 + 16.3).
/// </summary>
[TestFixture]
public class EscrowTests
{
    private string _tempFile = string.Empty;

    /// <summary>Creates an isolated escrow store file per test.</summary>
    [SetUp]
    public void SetUp()
    {
        this._tempFile = Path.Combine(Path.GetTempPath(), $"hnmo-escrow-test-{Guid.NewGuid():N}.json");
    }

    /// <summary>Cleans up the isolated temporary store file.</summary>
    [TearDown]
    public void TearDown()
    {
        if (File.Exists(this._tempFile))
        {
            File.Delete(this._tempFile);
        }
    }

    /// <summary>Verifies fee calculation across Seller, Buyer, and Split payers with min/max caps.</summary>
    [Test]
    public void FeeBreakdown_CoversSellerBuyerAndSplitWithMinMaxCaps()
    {
        var cfg = new EscrowConfiguration { FeePercent = 10, MinFee = 5, MaxFee = 50, FeePayer = EscrowFeePayer.Seller };
        var sellerLow = EscrowEngine.ComputeFee(cfg, 20);
        Assert.That(sellerLow.TotalFee, Is.EqualTo(5)); // MinFee applies (20 * 10% = 2 < 5)
        Assert.That(sellerLow.BuyerTotalDebit, Is.EqualTo(20));
        Assert.That(sellerLow.NetSellerProceeds, Is.EqualTo(15));

        cfg.FeePayer = EscrowFeePayer.Buyer;
        var buyerCap = EscrowEngine.ComputeFee(cfg, 1000);
        Assert.That(buyerCap.TotalFee, Is.EqualTo(50)); // MaxFee applies
        Assert.That(buyerCap.BuyerTotalDebit, Is.EqualTo(1050));
        Assert.That(buyerCap.NetSellerProceeds, Is.EqualTo(1000));

        cfg.FeePayer = EscrowFeePayer.Split;
        cfg.MaxFee = 0;
        var split = EscrowEngine.ComputeFee(cfg, 101); // 10.1 -> 10 total fee -> 5 buyer, 5 seller
        Assert.That(split.TotalFee, Is.EqualTo(10));
        Assert.That(split.BuyerFeePaid, Is.EqualTo(5));
        Assert.That(split.SellerFeePaid, Is.EqualTo(5));
        Assert.That(split.BuyerTotalDebit, Is.EqualTo(106));
        Assert.That(split.NetSellerProceeds, Is.EqualTo(96));
    }

    /// <summary>Verifies banned item, minimum account age, high-value confirmation, and self-target guards.</summary>
    [Test]
    public void Validation_RejectsBannedItems_AccountAge_HighValue_And_SelfTarget()
    {
        var cfg = new EscrowConfiguration
        {
            MinAccountAgeDays = 3,
            HighValueThresholdCoins = 500,
            BannedItems = ["13:20", "Forbidden Blade"],
        };
        var engine = new EscrowEngine(cfg, EscrowStore.Open(this._tempFile));
        var now = new DateTime(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);

        Assert.That(EscrowEngine.MeetsAccountAgeRequirement(cfg, now.AddDays(-1), now), Is.False);
        Assert.That(EscrowEngine.MeetsAccountAgeRequirement(cfg, now.AddDays(-4), now), Is.True);

        var banned = engine.CreateContract(
            "seller1",
            "CharA",
            string.Empty,
            [new EscrowItemSnapshot { Group = 13, Number = 20, Level = 0, NameEn = "Ring of Wizardry" }],
            0,
            null,
            EscrowCurrency.WCoinC,
            100,
            24,
            confirmHighValue: true,
            now);
        Assert.That(banned.Error, Is.EqualTo("BannedItem"));

        var hvRejected = engine.CreateContract(
            "seller1",
            "CharA",
            string.Empty,
            [new EscrowItemSnapshot { Group = 0, Number = 1, Level = 9, NameEn = "Short Sword" }],
            0,
            null,
            EscrowCurrency.WCoinC,
            600,
            24,
            confirmHighValue: false,
            now);
        Assert.That(hvRejected.Error, Is.EqualTo("NeedsHighValueConfirmation"));

        var selfRejected = engine.CreateContract(
            "seller1",
            "CharA",
            "CharA",
            [new EscrowItemSnapshot { Group = 0, Number = 1, Level = 9, NameEn = "Short Sword" }],
            0,
            null,
            EscrowCurrency.WCoinC,
            100,
            24,
            confirmHighValue: false,
            now);
        Assert.That(selfRejected.Error, Is.EqualTo("SelfTargetNotAllowed"));
    }

    /// <summary>Verifies 8 parallel buyers racing to accept one contract yields exactly one winner and zero duplication.</summary>
    [Test]
    public async Task Concurrency_ParallelBuyers_OnlyOneWins_NoDuplicationOrLoss()
    {
        var cfg = new EscrowConfiguration { FeePercent = 5, MinFee = 1, PostEscrowTradeCooldownHours = 24 };
        var engine = new EscrowEngine(cfg, EscrowStore.Open(this._tempFile));
        var now = new DateTime(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);

        var created = engine.CreateContract(
            "seller1",
            "SellerChar",
            string.Empty,
            [new EscrowItemSnapshot { Group = 0, Number = 22, Level = 13, NameEn = "Bone Blade", OriginSummary = "Boss drop" }],
            500_000,
            [new EscrowJewelLine { JewelKey = "JewelBless", Quantity = 10 }],
            EscrowCurrency.WCoinC,
            200,
            24,
            confirmHighValue: false,
            now);
        Assert.That(created.Error, Is.EqualTo("Ok"));
        var id = created.Contract!.ContractId;

        var tasks = Enumerable.Range(1, 8).Select(i => Task.Run(() =>
            engine.AcceptContractAtomic($"buyer{i}", $"BuyerChar{i}", id, confirmHighValue: true, now, _ => true)));
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        Assert.That(results.Count(r => r.Error == "Ok"), Is.EqualTo(1));
        Assert.That(results.Count(r => r.Error == "AlreadyClosed"), Is.EqualTo(7));

        var winner = results.Single(r => r.Error == "Ok").Contract!;
        Assert.That(engine.GetItemTradeLockUntil(winner.AcceptedByLogin, 0, 22, 13, now.AddHours(1)), Is.Not.Null);
        Assert.That(engine.GetItemTradeLockUntil(winner.AcceptedByLogin, 0, 22, 13, now.AddHours(25)), Is.Null);

        var ledgerCount = engine.Store.Read(d => d.FeeLedger.Count);
        Assert.That(ledgerCount, Is.EqualTo(1));
    }

    /// <summary>Verifies accept vs cancel and accept vs expiry are strictly mutually exclusive.</summary>
    [Test]
    public async Task Concurrency_AcceptVsCancel_And_AcceptVsExpiry_AreMutuallyExclusive()
    {
        var cfg = new EscrowConfiguration { FeePercent = 2, MinFee = 1 };
        var engine = new EscrowEngine(cfg, EscrowStore.Open(this._tempFile));
        var now = new DateTime(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);

        var c1 = engine.CreateContract(
            "seller1",
            "SellerChar",
            string.Empty,
            [new EscrowItemSnapshot { Group = 7, Number = 1, Level = 9, NameEn = "Dragon Helm" }],
            0,
            null,
            EscrowCurrency.Zen,
            1_000,
            12,
            confirmHighValue: false,
            now).Contract!;

        var acceptTask = Task.Run(() => engine.AcceptContractAtomic("buyer1", "Buyer", c1.ContractId, true, now, _ => true));
        var cancelTask = Task.Run(() => engine.CancelContract("seller1", c1.ContractId, false, "Changed mind", now));
        var (acceptRes, cancelRes) = (await acceptTask.ConfigureAwait(false), await cancelTask.ConfigureAwait(false));
        Assert.That((acceptRes.Error == "Ok") ^ (cancelRes.Error == "Ok"), Is.True);

        var c2 = engine.CreateContract(
            "seller2",
            "SellerChar2",
            string.Empty,
            [new EscrowItemSnapshot { Group = 8, Number = 1, Level = 9, NameEn = "Dragon Armor" }],
            0,
            null,
            EscrowCurrency.Zen,
            1_000,
            1,
            confirmHighValue: false,
            now).Contract!;

        var expiredRes = engine.AcceptContractAtomic("buyer2", "Buyer2", c2.ContractId, true, now.AddHours(2), _ => true);
        Assert.That(expiredRes.Error, Is.EqualTo("Expired"));
        Assert.That(engine.PeekOwedDeliveries("seller2"), Has.Count.EqualTo(1));
    }

    /// <summary>Verifies payment failure, mid-swap exception/crash rollback, and admin freeze enforcement.</summary>
    [Test]
    public void CrashMidSwap_PaymentFailureOrException_RollsBackCleanly()
    {
        var cfg = new EscrowConfiguration { FeePercent = 5, MinFee = 1 };
        var engine = new EscrowEngine(cfg, EscrowStore.Open(this._tempFile));
        var now = new DateTime(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);

        var created = engine.CreateContract(
            "seller1",
            "SellerChar",
            string.Empty,
            [new EscrowItemSnapshot { Group = 0, Number = 5, Level = 11, NameEn = "Blade" }],
            0,
            null,
            EscrowCurrency.WCoinC,
            100,
            24,
            confirmHighValue: false,
            now).Contract!;

        var insufficient = engine.AcceptContractAtomic("buyer1", "Buyer", created.ContractId, true, now, _ => false);
        Assert.That(insufficient.Error, Is.EqualTo("PaymentFailed"));

        Assert.Throws<InvalidOperationException>(() =>
            engine.AcceptContractAtomic("buyer1", "Buyer", created.ContractId, true, now, _ => throw new InvalidOperationException("Simulated crash mid-swap")));

        var stored = engine.Store.Read(d => d.Contracts.Single(c => c.ContractId == created.ContractId));
        Assert.That(stored.Status, Is.EqualTo(EscrowContractStatus.Open));
        Assert.That(engine.PeekOwedDeliveries("buyer1"), Is.Empty);
        Assert.That(engine.PeekOwedDeliveries("seller1"), Is.Empty);
        Assert.That(engine.Store.Read(d => d.FeeLedger), Is.Empty);

        // Freeze prevents acceptance until unfrozen.
        Assert.That(engine.SetFrozen(created.ContractId, true, "Audit"), Is.True);
        Assert.That(engine.AcceptContractAtomic("buyer1", "Buyer", created.ContractId, true, now, _ => true).Error, Is.EqualTo("Frozen"));
        Assert.That(engine.SetFrozen(created.ContractId, false, "Cleared"), Is.True);
        Assert.That(engine.AcceptContractAtomic("buyer1", "Buyer", created.ContractId, true, now, _ => true).Error, Is.EqualTo("Ok"));
    }
}
