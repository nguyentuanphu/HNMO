// <copyright file="TradeCreditEscrowTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System;
using System.IO;
using System.Linq;
using HNMO.GameLogic.PlugIns.Escrow;

/// <summary>
/// Unit &amp; accounting tests for <see cref="EscrowTradeCreditEngine"/> (ROADMAP 16.6):
/// highest-risk compliance gate, top-ups cannot cash out directly, 72h cash-sale proceeds hold,
/// 1-way coin conversion, one-time bank binding, daily/monthly caps, exact double-entry reconciliation
/// (`top-ups - conversions - cash-outs - fees`), mismatch alarm, and CSV export.
/// </summary>
[TestFixture]
public class TradeCreditEscrowTests
{
    private string _tempFile = string.Empty;

    /// <summary>Sets up isolated temporary store file per test.</summary>
    [SetUp]
    public void SetUp()
    {
        this._tempFile = Path.Combine(Path.GetTempPath(), $"hnmo-tc-{Guid.NewGuid():N}.json");
    }

    /// <summary>Deletes temporary store file after test.</summary>
    [TearDown]
    public void TearDown()
    {
        if (File.Exists(this._tempFile))
        {
            File.Delete(this._tempFile);
        }
    }

    /// <summary>Ensures Trade Credit is blocked when disabled or missing compliance acknowledgement.</summary>
    [Test]
    public void ComplianceGate_BlocksWithoutHighestRiskAcknowledgement()
    {
        var cfg = new EscrowConfiguration { Enabled = true, TradeCreditEnabled = true, TradeCreditComplianceAcknowledged = false };
        Assert.That(EscrowTradeCreditEngine.IsCompliantAndEnabled(cfg), Is.False);
        Assert.That(new EscrowEngine(cfg, EscrowStore.Open(this._tempFile)).IsCurrencyAllowed(EscrowCurrency.TradeCredit), Is.False);

        var tc = new EscrowTradeCreditEngine(cfg, EscrowStore.Open(this._tempFile));
        Assert.That(tc.TopUpCredit("buyer1", 100, "REF1", DateTime.UtcNow).Error, Is.EqualTo("TradeCreditDisabledOrUnacknowledged"));
    }

    /// <summary>Ensures top-ups can buy escrow contracts or convert to coins, NEVER cash out directly; only matured 72h proceeds cash out.</summary>
    [Test]
    public void ProceedsOnlyCashOut_HoldPeriod_Caps_BankLock_And_Reconciliation()
    {
        var cfg = new EscrowConfiguration
        {
            Enabled = true,
            TradeCreditEnabled = true,
            TradeCreditComplianceAcknowledged = true,
            AllowedCurrencies = [EscrowCurrency.TradeCredit],
            FeePercent = 5.0,
            MinFee = 1,
            TradeCreditVndPerUnit = 1000,
            TradeCreditToCoinRate = 2,
            TradeCreditCashOutHoldHours = 72,
            TradeCreditCashOutFeePercent = 5.0,
            TradeCreditCashOutMinFeeUnits = 5,
            TradeCreditCashOutDailyLimitUnits = 150,
            TradeCreditCashOutMonthlyLimitUnits = 250,
        };

        var store = EscrowStore.Open(this._tempFile);
        var core = new EscrowEngine(cfg, store);
        var tc = new EscrowTradeCreditEngine(cfg, store);
        var now = new DateTime(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);

        // 1. Buyer tops up 300 TC -> CANNOT cash out topped-up credits!
        Assert.That(tc.TopUpCredit("buyer1", 300, "VND-TOPUP-1", now).Error, Is.EqualTo("Ok"));
        Assert.That(tc.RegisterVerifiedBankOnce("buyer1", "970436", "VCB", "001122", "BUYER", now), Is.EqualTo("Ok"));
        Assert.That(tc.RequestCashOut("buyer1", 50, now.AddHours(100)).Error, Is.EqualTo("NoMaturedProceeds"));

        // 2. Seller creates TradeCredit contract for 200 TC (5% escrow fee = 10 TC, net seller proceeds = 190 TC)
        var created = core.CreateContract(
            "seller1",
            "CharS",
            string.Empty,
            [new EscrowItemSnapshot { Group = 0, Number = 22, Level = 13, NameEn = "Bone Blade" }],
            0,
            null,
            EscrowCurrency.TradeCredit,
            200,
            24,
            confirmHighValue: false,
            now).Contract!;

        var bought = tc.AcceptTradeCreditEscrow("buyer1", "CharB", created.ContractId, true, now);
        Assert.That(bought.Error, Is.EqualTo("Ok"));
        Assert.That(tc.GetAccount("buyer1").SpendableUnits, Is.EqualTo(100));
        Assert.That(tc.GetAccount("seller1").SpendableUnits, Is.EqualTo(190));

        // 3. Buyer converts remaining 100 TC -> 200 WCoinC one-way
        Assert.That(tc.ConvertToCoins("buyer1", 100, now).CoinsGranted, Is.EqualTo(200));
        Assert.That(core.PeekOwedDeliveries("buyer1").Any(o => o.Coins == 200), Is.True);

        // 4. Seller registers verified bank once (cannot overwrite!) and must wait 72h before cash-out
        Assert.That(tc.RegisterVerifiedBankOnce("seller1", "970407", "TCB", "19090011", "SELLER", now), Is.EqualTo("Ok"));
        Assert.That(tc.RegisterVerifiedBankOnce("seller1", "970422", "MB", "999999", "HACK", now), Is.EqualTo("BankAlreadyLocked"));
        Assert.That(tc.RequestCashOut("seller1", 100, now.AddHours(71)).Error, Is.EqualTo("NoMaturedProceeds"));

        // 5. After 72h, daily cap 150 rejects 160, accepts 100 (5 fee -> 95 net = 95,000 VND)
        var maturedAt = now.AddHours(73);
        Assert.That(tc.RequestCashOut("seller1", 160, maturedAt).Error, Is.EqualTo("DailyCashOutLimitExceeded"));
        var cashReq = tc.RequestCashOut("seller1", 100, maturedAt);
        Assert.That(cashReq.Error, Is.EqualTo("Ok"));
        Assert.That(cashReq.Request!.FeeUnits, Is.EqualTo(5));
        Assert.That(cashReq.Request.NetUnits, Is.EqualTo(95));
        Assert.That(cashReq.Request.NetVnd, Is.EqualTo(95_000));

        // 6. Reconciliation MUST balance with 0 variance both before AND after MarkCashOutPaid
        var preRec = tc.Reconcile(maturedAt);
        Assert.That(preRec.IsBalanced, Is.True);
        Assert.That(preRec.VarianceUnits, Is.EqualTo(0));

        Assert.That(tc.MarkCashOutPaid(cashReq.Request.RequestId, "BANK-PAY-1", maturedAt.AddHours(1)).Error, Is.EqualTo("Ok"));
        var postRec = tc.Reconcile(maturedAt.AddHours(1));
        Assert.That(postRec.IsBalanced, Is.True);
        Assert.That(postRec.ActualCirculatingUnits, Is.EqualTo(90));
        Assert.That(postRec.ExpectedCirculatingUnits, Is.EqualTo(90)); // 300 topup - 100 convert - 95 cashout - (10+5 fees) = 90!

        // 7. CSV export contains headers & reconciliation footer; tampering triggers mismatch alarm!
        var csv = tc.ExportCsv(maturedAt.AddHours(2));
        Assert.That(csv, Does.Contain("EntryId,TimestampUtc,TimestampSydney"));
        Assert.That(csv, Does.Contain("RECONCILIATION,"));

        store.Mutate(d =>
        {
            d.TradeCreditAccounts["seller1"].GeneralUnits += 50; // Simulate corrupt/unledgered credit
            return (true, true);
        });
        var mismatch = tc.Reconcile(maturedAt.AddHours(3));
        Assert.That(mismatch.IsBalanced, Is.False);
        Assert.That(mismatch.VarianceUnits, Is.EqualTo(50));
    }
}
