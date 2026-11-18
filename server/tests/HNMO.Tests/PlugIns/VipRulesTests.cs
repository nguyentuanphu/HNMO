// <copyright file="VipRulesTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.Vip;

/// <summary>VIP quote, subscription, replay and unified-state rules; these tests do not simulate real wallet integration.</summary>
[TestFixture]
public class VipRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc);
    private VipConfiguration _configuration = null!;
    private VipRules _rules = null!;

    /// <summary>Creates isolated enabled rules; the actual configuration defaults remain disabled.</summary>
    [SetUp]
    public void SetUp()
    {
        this._configuration = new VipConfiguration { Enabled = true };
        this._rules = new VipRules(this._configuration, VipStateStore.CreateInMemory());
    }

    /// <summary>VIP is opt-in and quoting does not activate a subscription or change an account balance.</summary>
    [Test]
    public void DisabledByDefaultAndQuotesHaveNoSideEffects()
    {
        var defaults = new VipConfiguration();
        var disabled = new VipRules(defaults, VipStateStore.CreateInMemory());
        Assert.That(disabled.Quote("ann", 1, 7, 1000, Now).Result, Is.EqualTo(VipRuleResult.Disabled));
        var quote = this._rules.Quote("ann", 1, 7, 1000, Now);
        Assert.That(quote.Result, Is.EqualTo(VipRuleResult.Ok));
        Assert.That(quote.Plan!.UntilUtc, Is.EqualTo(Now.AddDays(7)));
        Assert.That(this._rules.Active("ann", Now), Is.Null);
    }

    /// <summary>Insufficient balances and durations not actually offered are rejected.</summary>
    [Test]
    public void RejectsInsufficientCoinsUnknownLevelsAndDurations()
    {
        Assert.That(this._rules.Quote("ann", 1, 7, 99, Now).Result, Is.EqualTo(VipRuleResult.InsufficientCoins));
        Assert.That(this._rules.Quote("ann", 1, 8, 1000, Now).Result, Is.EqualTo(VipRuleResult.UnknownDuration));
        Assert.That(this._rules.Quote("ann", 99, 7, 1000, Now).Result, Is.EqualTo(VipRuleResult.UnknownLevel));
    }

    /// <summary>Each offered duration produces its exact expiry without changing the stored subscription.</summary>
    /// <param name="days">An offered duration.</param>
    [TestCase(7)]
    [TestCase(30)]
    [TestCase(90)]
    public void QuotesEveryDefaultDuration(int days)
        => Assert.That(this._rules.Quote("ann", 1, days, 10000, Now).Plan!.UntilUtc, Is.EqualTo(Now.AddDays(days)));

    /// <summary>Recording an independently confirmed receipt extends time exactly once.</summary>
    [Test]
    public void ConfirmedReceiptReplayDoesNotExtendAgain()
    {
        var first = this._rules.Quote("ann", 1, 7, 1000, Now).Plan!;
        var receipt = Guid.NewGuid();
        this._rules.RecordConfirmedPayment(first, receipt, Now);
        this._rules.RecordConfirmedPayment(first, receipt, Now.AddMinutes(1));
        Assert.That(this._rules.Active("ANN", Now)!.UntilUtc, Is.EqualTo(Now.AddDays(7)));
        var extension = this._rules.Quote("ann", 1, 30, 1000, Now.AddDays(1)).Plan!;
        this._rules.RecordConfirmedPayment(extension, Guid.NewGuid(), Now.AddDays(1));
        Assert.That(this._rules.Active("ann", Now.AddDays(1))!.UntilUtc, Is.EqualTo(Now.AddDays(37)));
    }

    /// <summary>Changes to a quote, price or existing subscription cannot silently fulfill an old receipt.</summary>
    [Test]
    public void RejectsTamperedAndStaleQuotes()
    {
        var plan = this._rules.Quote("ann", 1, 7, 1000, Now).Plan!;
        Assert.Throws<InvalidOperationException>(() => this._rules.RecordConfirmedPayment(plan with { UntilUtc = Now.AddYears(1) }, Guid.NewGuid(), Now));
        Assert.Throws<InvalidOperationException>(() => this._rules.RecordConfirmedPayment(plan with { Price = 0 }, Guid.NewGuid(), Now));
        this._rules.Grant("ann", 1, Now.AddDays(1), "host", "Verified support request", Now);
        Assert.Throws<InvalidOperationException>(() => this._rules.RecordConfirmedPayment(plan, Guid.NewGuid(), Now));
    }

    /// <summary>A receipt cannot be reused for another account or silently resurrect a removed subscription.</summary>
    [Test]
    public void ReceiptCannotBeTransferredOrResurrectRemovedMembership()
    {
        var receipt = Guid.NewGuid();
        var plan = this._rules.Quote("ann", 1, 7, 1000, Now).Plan!;
        this._rules.RecordConfirmedPayment(plan, receipt, Now);
        Assert.Throws<InvalidOperationException>(() => this._rules.RecordConfirmedPayment(plan with { LoginName = "BOB" }, receipt, Now));
        this._rules.Remove("ann", "host", "Verified removal", Now);
        Assert.That(this._rules.RecordConfirmedPayment(plan, receipt, Now), Is.Null);
    }

    /// <summary>Benefit values become unavailable at the exact expiry or when the host disables the feature.</summary>
    [Test]
    public void BenefitValuesFollowExpiryAndEnablement()
    {
        this._rules.Grant("ann", 1, Now.AddDays(1), "host", "Verified grant", Now);
        Assert.That(this._rules.BenefitsAt("ann", Now.AddTicks(-1)), Is.Null);
        Assert.That(this._rules.BenefitsAt("ann", Now)!.ExperiencePercent, Is.EqualTo(5));
        Assert.That(this._rules.BenefitsAt("ann", Now.AddDays(1)), Is.Null);
        this._configuration.Enabled = false;
        Assert.That(this._rules.BenefitsAt("ann", Now), Is.Null);
    }

    /// <summary>Expired time does not extend a new purchase, and active levels cannot be downgraded accidentally.</summary>
    [Test]
    public void ExpiredMembershipStartsFromNowAndActiveTierChangeIsExplicit()
    {
        this._rules.Grant("ann", 2, Now.AddDays(1), "host", "Verified grant", Now);
        Assert.That(this._rules.Quote("ann", 1, 7, 1000, Now).Result, Is.EqualTo(VipRuleResult.DifferentActiveLevel));
        Assert.That(this._rules.Quote("ann", 1, 7, 1000, Now.AddDays(2)).Plan!.UntilUtc, Is.EqualTo(Now.AddDays(9)));
    }

    /// <summary>Daily bookkeeping rolls at Sydney midnight and cannot be replayed by removing and regranting VIP.</summary>
    [Test]
    public void DailyBonusUsesSydneyDaysAndSurvivesRegrant()
    {
        var beforeMidnight = new DateTime(2026, 11, 16, 12, 59, 0, DateTimeKind.Utc);
        this._rules.Grant("ann", 1, Now.AddDays(7), "host", "Verified grant", Now);
        Assert.That(this._rules.RecordGrantedDailyBonus("ann", beforeMidnight), Is.True);
        Assert.That(this._rules.RecordGrantedDailyBonus("ann", beforeMidnight), Is.False);
        Assert.That(this._rules.CanClaimDaily("ann", beforeMidnight.AddMinutes(1)), Is.True);
        this._rules.Remove("ann", "host", "Verified removal", beforeMidnight);
        this._rules.Grant("ann", 1, Now.AddDays(7), "host", "Verified grant", beforeMidnight);
        Assert.That(this._rules.CanClaimDaily("ann", beforeMidnight), Is.False);
        Assert.That(this._rules.CanClaimDaily("ann", beforeMidnight.AddDays(-1)), Is.False);
    }

    /// <summary>Administrative changes require a recorded identity/reason and real future UTC expiry.</summary>
    [Test]
    public void AdministrativeGrantAndRemoveRequireReasons()
    {
        Assert.Throws<ArgumentException>(() => this._rules.Grant("ann", 1, Now.AddDays(1), "host", "", Now));
        Assert.Throws<ArgumentException>(() => this._rules.Grant("ann", 1, Now, "host", "Verified", Now));
        this._rules.Grant("ann", 1, Now.AddDays(1), "host", "Verified", Now);
        Assert.Throws<ArgumentException>(() => this._rules.Remove("ann", "", "Verified", Now));
        Assert.That(this._rules.Remove("ann", "host", "Verified", Now), Is.True);
        Assert.That(this._rules.Active("ann", Now), Is.Null);
    }

    /// <summary>Invalid configuration fails closed instead of offering broken purchases.</summary>
    [Test]
    public void InvalidConfigurationAndNonUtcTimeAreRejected()
    {
        this._configuration.Levels[0].Offers.Add(this._configuration.Levels[0].Offers[0]);
        Assert.That(this._rules.Quote("ann", 1, 7, 1000, Now).Result, Is.EqualTo(VipRuleResult.Invalid));
        Assert.Throws<ArgumentException>(() => this._rules.Quote("ann", 1, 7, 1000, DateTime.SpecifyKind(Now, DateTimeKind.Unspecified)));
    }

    /// <summary>VIP persists in the unified file, keeps other sections and retains audit/replay data after reopening.</summary>
    [Test]
    public void UnifiedStateRoundTripPreservesOtherSectionsAndAudit()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"hnmo-vip-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, HnmoStateStore.StateFileName);
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        try
        {
            var backend = new HnmoStateStore(path);
            backend.WriteSection("existingFeature", new { value = 42 }, options);
            var rules = new VipRules(this._configuration, VipStateStore.Open(backend));
            rules.Grant("ann", 1, Now.AddDays(1), "host", "Verified grant", Now);
            var reopened = new HnmoStateStore(path);
            var state = reopened.ReadSection<VipState>("vip", options)!;
            Assert.That(state.Memberships["ANN"].UntilUtc, Is.EqualTo(Now.AddDays(1)));
            Assert.That(state.Audit.Single().Reason, Is.EqualTo("Verified grant"));
            Assert.That(reopened.ReadSection<JsonElement>("existingFeature", options).GetProperty("value").GetInt32(), Is.EqualTo(42));
            Assert.That(Directory.GetFiles(directory).Select(Path.GetFileName), Is.All.StartsWith(HnmoStateStore.StateFileName));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    /// <summary>A corrupt existing VIP section is preserved and rejected rather than silently replaced.</summary>
    [Test]
    public void CorruptVipSectionFailsClosed()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"hnmo-vip-corrupt-{Guid.NewGuid():N}");
        try
        {
            var backend = new HnmoStateStore(Path.Combine(directory, HnmoStateStore.StateFileName));
            backend.WriteSection("vip", "invalid subscription data", new JsonSerializerOptions());
            Assert.Throws<InvalidDataException>(() => VipStateStore.Open(backend));
            Assert.That(backend.ReadSection<string>("vip", new JsonSerializerOptions()), Is.EqualTo("invalid subscription data"));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
