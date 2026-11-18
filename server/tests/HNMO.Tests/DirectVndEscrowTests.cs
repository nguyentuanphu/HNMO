// <copyright file="DirectVndEscrowTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System;
using System.IO;
using System.Linq;
using HNMO.GameLogic.PlugIns.Escrow;

/// <summary>
/// Unit and security tests for <see cref="EscrowDirectVndEngine"/> (ROADMAP 16.4).
/// </summary>
[TestFixture]
public class DirectVndEscrowTests
{
    private string _tempFile = string.Empty;

    /// <summary>Sets up isolated store file per test.</summary>
    [SetUp]
    public void SetUp()
    {
        this._tempFile = Path.Combine(Path.GetTempPath(), $"hnmo-direct-vnd-{Guid.NewGuid():N}.json");
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

    /// <summary>Ensures Direct P2P VND stays blocked unless BOTH Enabled and ComplianceAcknowledged are true.</summary>
    [Test]
    public void ComplianceGate_RequiresEnabledAndLegalAcknowledgement()
    {
        var cfg = new EscrowConfiguration { Enabled = true, DirectVndEnabled = true, DirectVndComplianceAcknowledged = false };
        Assert.That(EscrowDirectVndEngine.IsCompliantAndEnabled(cfg), Is.False);

        var engine = new EscrowDirectVndEngine(cfg, EscrowStore.Open(this._tempFile));
        var res = engine.CreateDirectVndContract(
            "seller1",
            "CharS",
            string.Empty,
            [new EscrowItemSnapshot { Group = 0, Number = 1, Level = 9, NameEn = "Kris" }],
            0,
            null,
            500_000,
            "970436",
            "Vietcombank",
            "0123456789",
            "NGUYEN VAN A",
            0,
            null,
            24,
            confirmHighValue: true,
            DateTime.UtcNow);
        Assert.That(res.Error, Is.EqualTo("DirectVndDisabledOrUnacknowledged"));
    }

    /// <summary>Ensures seller bank account is redacted until buyer reserves the contract.</summary>
    [Test]
    public void PrivacyRedaction_HidesBankDetailsUntilAccepted()
    {
        var cfg = new EscrowConfiguration
        {
            Enabled = true,
            DirectVndEnabled = true,
            DirectVndComplianceAcknowledged = true,
            DirectVndServiceFeeCoins = 10,
            DirectVndFeePayer = EscrowFeePayer.Split,
        };
        var engine = new EscrowDirectVndEngine(cfg, EscrowStore.Open(this._tempFile));
        var now = new DateTime(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);

        var created = engine.CreateDirectVndContract(
            "seller1",
            "CharS",
            string.Empty,
            [new EscrowItemSnapshot { Group = 0, Number = 22, Level = 13, NameEn = "Bone Blade" }],
            0,
            null,
            500_000,
            "970436",
            "Vietcombank",
            "0998877665",
            "NGUYEN VAN A",
            50,
            "whsec-123",
            24,
            confirmHighValue: true,
            now).Contract!;

        var publicBefore = EscrowDirectVndEngine.ProjectForViewer(created, "otherBuyer", isAdmin: false);
        Assert.That(publicBefore.DirectVnd!.BankAccountNumber, Is.Empty);
        Assert.That(publicBefore.DirectVnd.PaymentReference, Is.Empty);
        Assert.That(publicBefore.DirectVnd.WebhookSecret, Is.Empty);

        var reserved = engine.ReserveForPayment("buyer1", "CharB", created.ContractId, now, _ => true);
        Assert.That(reserved.Error, Is.EqualTo("Ok"));
        Assert.That(reserved.Contract!.Status, Is.EqualTo(EscrowContractStatus.ReservedForPayment));

        var buyerAfter = EscrowDirectVndEngine.ProjectForViewer(reserved.Contract, "buyer1", isAdmin: false);
        Assert.That(buyerAfter.DirectVnd!.BankAccountNumber, Is.EqualTo("0998877665"));
        Assert.That(buyerAfter.DirectVnd.PaymentReference, Does.StartWith("HNMO-"));
        Assert.That(buyerAfter.DirectVnd.WebhookSecret, Is.Empty);
    }

    /// <summary>Verifies single payment-window extension, image magic-byte validation, and manual seller completion.</summary>
    [Test]
    public void PaymentWindow_ExtendOnce_ReceiptValidation_And_ManualConfirm()
    {
        var cfg = new EscrowConfiguration
        {
            Enabled = true,
            DirectVndEnabled = true,
            DirectVndComplianceAcknowledged = true,
            DirectVndPaymentWindowMinutes = 30,
            DirectVndExtendMinutes = 15,
            DirectVndMaxReceiptBytes = 64 * 1024,
        };
        var engine = new EscrowDirectVndEngine(cfg, EscrowStore.Open(this._tempFile));
        var now = new DateTime(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);

        var c = engine.CreateDirectVndContract(
            "seller1",
            "CharS",
            string.Empty,
            [new EscrowItemSnapshot { Group = 7, Number = 1, Level = 11, NameEn = "Dragon Helm" }],
            0,
            null,
            300_000,
            "970407",
            "Techcombank",
            "190300011122",
            "TRAN THI B",
            25,
            null,
            24,
            confirmHighValue: true,
            now).Contract!;

        engine.ReserveForPayment("buyer1", "CharB", c.ContractId, now, _ => true);
        Assert.That(engine.ExtendPaymentWindowOnce("buyer1", c.ContractId, now.AddMinutes(10)).Error, Is.EqualTo("Ok"));
        Assert.That(engine.ExtendPaymentWindowOnce("buyer1", c.ContractId, now.AddMinutes(12)).Error, Is.EqualTo("AlreadyExtended"));

        // Reject spoofed EXE disguised as image/png
        var badImg = Convert.ToBase64String(new byte[] { 0x4D, 0x5A, 0x90, 0x00, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
        Assert.That(engine.MarkBuyerPaid("buyer1", c.ContractId, "image/png", badImg, "paid", now).Error, Is.EqualTo("InvalidReceiptImage"));

        // Valid PNG magic header succeeds
        var validPng = Convert.ToBase64String(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4, 5, 6 });
        Assert.That(engine.MarkBuyerPaid("buyer1", c.ContractId, "image/png", validPng, "paid", now).Error, Is.EqualTo("Ok"));

        var done = engine.SellerConfirmMoneyReceived("seller1", c.ContractId, now.AddMinutes(15));
        Assert.That(done.Error, Is.EqualTo("Ok"));
        Assert.That(done.Contract!.Status, Is.EqualTo(EscrowContractStatus.Completed));

        var core = new EscrowEngine(cfg, EscrowStore.Open(this._tempFile));
        Assert.That(core.PeekOwedDeliveries("buyer1").Any(o => o.Items.Count == 1), Is.True);
        Assert.That(core.PeekOwedDeliveries("seller1").Any(o => o.Coins == 25), Is.True);
    }

    /// <summary>Verifies signed HMAC webhook checks signature, timestamp skew, replay nonce, amount, and reference.</summary>
    [Test]
    public void SignedWebhook_EnforcesHmacReplayAmountAndReference()
    {
        var cfg = new EscrowConfiguration
        {
            Enabled = true,
            DirectVndEnabled = true,
            DirectVndComplianceAcknowledged = true,
        };
        var engine = new EscrowDirectVndEngine(cfg, EscrowStore.Open(this._tempFile));
        var now = new DateTime(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);

        var c = engine.CreateDirectVndContract(
            "seller1",
            "CharS",
            string.Empty,
            [new EscrowItemSnapshot { Group = 0, Number = 5, Level = 9, NameEn = "Blade" }],
            0,
            null,
            250_000,
            "970422",
            "MB Bank",
            "0011223344",
            "LE VAN C",
            0,
            "super-secret-key",
            24,
            confirmHighValue: true,
            now).Contract!;

        engine.ReserveForPayment("buyer1", "CharB", c.ContractId, now, _ => true);
        var payRef = c.DirectVnd!.PaymentReference;
        var ts = new DateTimeOffset(now).ToUnixTimeSeconds();
        var body = $"{{\"amount\":250000,\"reference\":\"{payRef}\"}}";
        var validSig = EscrowDirectVndEngine.ComputeWebhookSignature("super-secret-key", ts, "nonce-1", body);

        Assert.That(engine.VerifyAndCompleteWebhook(c.ContractId, body, "bad-sig", ts, "nonce-1", 250_000, payRef, now).Error, Is.EqualTo("InvalidSignature"));
        Assert.That(engine.VerifyAndCompleteWebhook(c.ContractId, body, validSig, ts, "nonce-1", 200_000, payRef, now).Error, Is.EqualTo("AmountOrReferenceMismatch"));
        Assert.That(engine.VerifyAndCompleteWebhook(c.ContractId, body, validSig, ts, "nonce-1", 250_000, "WRONG", now).Error, Is.EqualTo("AmountOrReferenceMismatch"));
        Assert.That(engine.VerifyAndCompleteWebhook(c.ContractId, body, validSig, ts, "nonce-1", 250_000, payRef, now).Error, Is.EqualTo("Ok"));

        // Replay / duplicate after completion must fail
        var replay = engine.VerifyAndCompleteWebhook(c.ContractId, body, validSig, ts, "nonce-1", 250_000, payRef, now);
        Assert.That(replay.Error, Is.AnyOf("ReplayDetected", "InvalidState"));
    }

    /// <summary>Verifies admin dispute decision forfeits seller bond on buyer award and penalizes losing party reputation.</summary>
    [Test]
    public void DisputeResolution_ForfeitsBondAndPenalizesLoserReputation()
    {
        var cfg = new EscrowConfiguration
        {
            Enabled = true,
            DirectVndEnabled = true,
            DirectVndComplianceAcknowledged = true,
            DirectVndAutoDisputeHours = 2,
        };
        var engine = new EscrowDirectVndEngine(cfg, EscrowStore.Open(this._tempFile));
        var now = new DateTime(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);

        var c = engine.CreateDirectVndContract(
            "seller1",
            "CharS",
            string.Empty,
            [new EscrowItemSnapshot { Group = 8, Number = 1, Level = 13, NameEn = "Dragon Armor" }],
            0,
            null,
            900_000,
            "970436",
            "Vietcombank",
            "0111222333",
            "PHAM D",
            100,
            null,
            24,
            confirmHighValue: true,
            now).Contract!;

        engine.ReserveForPayment("buyer1", "CharB", c.ContractId, now, _ => true);
        var validPng = Convert.ToBase64String(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4, 5, 6 });
        engine.MarkBuyerPaid("buyer1", c.ContractId, "image/png", validPng, "transferred", now.AddMinutes(5));

        // Auto-escalates after 2 hours
        Assert.That(engine.ProcessTimeouts(now.AddHours(3)), Is.EqualTo(1));

        var resolved = engine.ResolveDispute(c.ContractId, awardToBuyer: true, "Valid receipt confirmed", now.AddHours(4));
        Assert.That(resolved.Error, Is.EqualTo("Ok"));
        Assert.That(resolved.LosingLogin, Is.EqualTo("seller1"));
        Assert.That(resolved.Contract!.DirectVnd!.BondForfeited, Is.True);

        var core = new EscrowEngine(cfg, EscrowStore.Open(this._tempFile));
        Assert.That(core.PeekOwedDeliveries("seller1"), Is.Empty);
        Assert.That(core.PeekOwedDeliveries("buyer1").Any(o => o.Items.Count == 1), Is.True);
        Assert.That(core.GetReputation("seller1").DisputesLost, Is.EqualTo(1));
    }
}
