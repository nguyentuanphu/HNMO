// <copyright file="HostVndEscrowTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System;
using System.IO;
using System.Linq;
using HNMO.GameLogic.PlugIns.Escrow;

/// <summary>
/// Unit tests for <see cref="EscrowHostVndEngine"/> (ROADMAP 16.2):
/// regulatory gate, fee min/max calculation, Casso/SePay memo matching,
/// idempotent item release, payout queue within N days, and dispute window rulings.
/// </summary>
[TestFixture]
public class HostVndEscrowTests
{
    private string _tempFile = string.Empty;

    /// <summary>Sets up isolated store file per test.</summary>
    [SetUp]
    public void SetUp()
    {
        this._tempFile = Path.Combine(Path.GetTempPath(), $"hnmo-host-vnd-{Guid.NewGuid():N}.json");
    }

    /// <summary>Cleans up temporary store file.</summary>
    [TearDown]
    public void TearDown()
    {
        if (File.Exists(this._tempFile))
        {
            File.Delete(this._tempFile);
        }
    }

    /// <summary>Ensures Host VND mode stays blocked until BOTH enabled and legally acknowledged.</summary>
    [Test]
    public void ComplianceGate_BlocksWithoutLegalAcknowledgement()
    {
        var cfg = new EscrowConfiguration
        {
            Enabled = true,
            HostVndEnabled = true,
            HostVndComplianceAcknowledged = false,
            HostVndBankAccountNumber = "99998888",
        };
        Assert.That(EscrowHostVndEngine.IsCompliantAndEnabled(cfg), Is.False);
        var engine = new EscrowHostVndEngine(cfg, EscrowStore.Open(this._tempFile));
        var res = engine.CreateHostVndContract(
            "seller1",
            "CharS",
            string.Empty,
            [new EscrowItemSnapshot { Group = 0, Number = 1, Level = 9, NameEn = "Sword" }],
            0,
            null,
            500_000,
            "970436",
            "VCB",
            "0123456",
            "SELLER",
            24,
            true,
            DateTime.UtcNow);
        Assert.That(res.Error, Is.EqualTo("HostVndDisabledOrUnacknowledged"));
    }

    /// <summary>Verifies Host VND commission fee math with min and max VND caps and bank memo reference extraction.</summary>
    [Test]
    public void CommissionFee_And_ReferenceExtraction_WorkAcrossBankFormats()
    {
        var cfg = new EscrowConfiguration { HostVndFeePercent = 5.0, HostVndMinFeeVnd = 5000, HostVndMaxFeeVnd = 50_000 };
        Assert.That(EscrowHostVndEngine.ComputeHostFeeVnd(cfg, 50_000), Is.EqualTo((5000L, 45_000L)));
        Assert.That(EscrowHostVndEngine.ComputeHostFeeVnd(cfg, 500_000), Is.EqualTo((25_000L, 475_000L)));
        Assert.That(EscrowHostVndEngine.ComputeHostFeeVnd(cfg, 2_000_000), Is.EqualTo((50_000L, 1_950_000L)));

        Assert.That(EscrowHostVndEngine.ExtractHnmoReference("MBVCB.1234.HNMO-A1B2C3D4.CT"), Is.EqualTo("HNMO-A1B2C3D4"));
        Assert.That(EscrowHostVndEngine.ExtractHnmoReference("Chuyen khoan HNMOA1B2C3D4"), Is.EqualTo("HNMO-A1B2C3D4"));
    }

    /// <summary>Verifies full Host VND payment flow: reserve, webhook release, ledger entry, payout queue, dispute, and resolution.</summary>
    [Test]
    public void HostPaymentLifecycle_ReleasesItem_QueuesPayout_AndHandlesDisputes()
    {
        var cfg = new EscrowConfiguration
        {
            Enabled = true,
            HostVndEnabled = true,
            HostVndComplianceAcknowledged = true,
            HostVndBankBin = "970436",
            HostVndBankName = "Vietcombank",
            HostVndBankAccountNumber = "8888777766",
            HostVndBankAccountName = "HNMO HOST",
            HostVndFeePercent = 5.0,
            HostVndMinFeeVnd = 5000,
            HostVndPayoutDays = 3,
            HostVndDisputeWindowHours = 24,
        };
        var store = EscrowStore.Open(this._tempFile);
        var host = new EscrowHostVndEngine(cfg, store);
        var now = new DateTime(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);

        var created = host.CreateHostVndContract(
            "seller1",
            "CharS",
            string.Empty,
            [new EscrowItemSnapshot { Group = 7, Number = 22, Level = 13, NameEn = "Bone Helm" }],
            0,
            null,
            600_000,
            "970407",
            "Techcombank",
            "1909001122",
            "SELLER NAME",
            24,
            true,
            now).Contract!;

        // Seller bank account hidden from other viewers
        var redacted = EscrowHostVndEngine.ProjectForViewer(created, "buyer1", isAdmin: false);
        Assert.That(redacted.HostVnd!.SellerBankAccountNumber, Is.Empty);

        var res = host.ReserveForHostPayment("buyer1", "CharB", created.ContractId, now);
        Assert.That(res.Error, Is.EqualTo("Ok"));
        var payRef = res.Contract!.HostVnd!.PaymentReference;

        // Underpaid bank webhook rejected cleanly
        Assert.That(host.ConfirmHostPaymentReceived($"CK {payRef}", 100_000, "casso:1", now).Error, Is.EqualTo("Underpaid"));

        // Exact payment releases item, queues seller payout minus 5% (30,000 VND), records VND fee in ledger
        var confirmed = host.ConfirmHostPaymentReceived($"MB.{payRef}.OK", 600_000, "sepay:991", now.AddMinutes(5));
        Assert.That(confirmed.Error, Is.EqualTo("Ok"));
        Assert.That(confirmed.Contract!.Status, Is.EqualTo(EscrowContractStatus.Completed));
        Assert.That(confirmed.Contract.HostVnd!.NetSellerPayoutVnd, Is.EqualTo(570_000));
        Assert.That(confirmed.Contract.HostVnd.PayoutStatus, Is.EqualTo("Queued"));

        // Duplicate webhook is idempotent
        Assert.That(host.ConfirmHostPaymentReceived(payRef, 600_000, "sepay:991", now.AddMinutes(6)).Error, Is.EqualTo("AlreadyProcessed"));

        var core = new EscrowEngine(cfg, store);
        Assert.That(core.PeekOwedDeliveries("buyer1").Any(o => o.Items.Count == 1), Is.True);
        Assert.That(store.Read(d => d.FeeLedger.Any(f => f.Currency == "VND" && f.FeeAmount == 30_000)), Is.True);

        // Open dispute inside 24h window blocks payout until admin clears it
        Assert.That(host.OpenHostDispute("buyer1", created.ContractId, "Check item options", now.AddHours(2)).Error, Is.EqualTo("Ok"));
        Assert.That(host.MarkPayoutPaid(created.ContractId, "BANK-TX-1", now.AddDays(3)).Error, Is.EqualTo("NotQueuedForPayout"));

        var resolved = host.ResolveHostDispute(created.ContractId, releasePayoutToSeller: true, "Verified legit", now.AddHours(4));
        Assert.That(resolved.Error, Is.EqualTo("Ok"));
        Assert.That(host.MarkPayoutPaid(created.ContractId, "BANK-TX-1", now.AddDays(3)).Error, Is.EqualTo("Ok"));
    }
}
