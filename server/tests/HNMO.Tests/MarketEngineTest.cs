// <copyright file="MarketEngineTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Threading.Tasks;
using HNMO.GameLogic.PlugIns.ItemMarket;

/// <summary>
/// Unit and concurrency tests for the Item Market escrow engine (<see cref="MarketEngine"/>).
/// </summary>
[TestFixture]
public class MarketEngineTest
{
    private string _tempDir = string.Empty;

    /// <summary>Creates an isolated temp directory per test.</summary>
    [SetUp]
    public void SetUp()
    {
        this._tempDir = Path.Combine(Path.GetTempPath(), $"hnmo-mkt-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this._tempDir);
    }

    /// <summary>Cleans up temp files.</summary>
    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(this._tempDir))
        {
            Directory.Delete(this._tempDir, recursive: true);
        }
    }

    /// <summary>
    /// Ten concurrent buyers racing on the same listing yield exactly ONE winner;
    /// every other buyer receives <see cref="MarketClaimResult.AlreadySoldOrMissing"/>.
    /// </summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task ConcurrentBuyClaimsAllowExactlyOneWinner()
    {
        var engine = this.CreateEngine();
        var listed = engine.CreateListing("seller1", "Knight", SampleSword(), MarketCurrency.WCoinC, 500, DateTime.UtcNow, out var listing);
        Assert.That(listed, Is.EqualTo(MarketListResult.Success));
        Assert.That(listing, Is.Not.Null);

        var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(1, 10).Select(i => Task.Run(async () =>
        {
            await startGate.Task.ConfigureAwait(false);
            return engine.TryClaimForBuy($"buyer{i}", listing!.Id, DateTime.UtcNow);
        })).ToArray();

        startGate.SetResult();
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        Assert.That(results.Count(r => r.Result == MarketClaimResult.Claimed), Is.EqualTo(1));
        Assert.That(results.Count(r => r.Result == MarketClaimResult.AlreadySoldOrMissing), Is.EqualTo(9));
    }

    /// <summary>Banned items and disabled currencies are rejected cleanly.</summary>
    [Test]
    public void BannedItemsAndDisabledCurrenciesAreRejected()
    {
        var cfg = new MarketConfiguration
        {
            FileName = Path.Combine(this._tempDir, "hnmo-state.json"),
            AllowWCoinC = true,
            AllowWCoinP = false,
            BannedItems = ["14:14"],
        };
        var engine = new MarketEngine(cfg, MarketStore.Open(cfg.FileName), enabled: true);

        var banned = engine.CreateListing("seller", "Knight", new MarketItemSnapshot { Group = 14, Number = 14, NameEn = "Jewel of Soul" }, MarketCurrency.WCoinC, 50, DateTime.UtcNow, out _);
        var disabledCur = engine.CreateListing("seller", "Knight", SampleSword(), MarketCurrency.WCoinP, 50, DateTime.UtcNow, out _);
        Assert.That(banned, Is.EqualTo(MarketListResult.ItemBanned));
        Assert.That(disabledCur, Is.EqualTo(MarketListResult.CurrencyDisabled));
    }

    /// <summary>Fees, seller self-buy block, rollback restore, escrow delivery, expiry, and audit log work end-to-end.</summary>
    [Test]
    public void EscrowLifecycleFeeRollbackAndExpiry()
    {
        var engine = this.CreateEngine(cfg =>
        {
            cfg.FeePercent = 5.0;
            cfg.MaxListingsPerAccount = 2;
            cfg.ExpiryHours = 24;
        });

        var now = new DateTime(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc);
        Assert.That(engine.CreateListing("alice", "Alice", SampleSword(), MarketCurrency.Zen, 1000, now, out var l1), Is.EqualTo(MarketListResult.Success));
        Assert.That(engine.CreateListing("alice", "Alice", SampleSword(), MarketCurrency.WCoinC, 200, now, out var l2), Is.EqualTo(MarketListResult.Success));
        Assert.That(engine.CreateListing("alice", "Alice", SampleSword(), MarketCurrency.WCoinC, 300, now, out _), Is.EqualTo(MarketListResult.ListingLimitReached));

        // Seller cannot buy own listing
        Assert.That(engine.TryClaimForBuy("alice", l1!.Id, now).Result, Is.EqualTo(MarketClaimResult.CannotBuyOwnListing));

        // Payment failure rollback restores the listing
        var claim1 = engine.TryClaimForBuy("bob", l1.Id, now);
        Assert.That(claim1.Result, Is.EqualTo(MarketClaimResult.Claimed));
        Assert.That(claim1.FeeAmount, Is.EqualTo(50));
        Assert.That(claim1.NetAmount, Is.EqualTo(950));
        engine.RestoreClaimedListing(claim1.Listing!);

        // Successful buy moves item to buyer delivery & Zen payout to seller
        var claim2 = engine.TryClaimForBuy("bob", l1.Id, now.AddMinutes(5));
        Assert.That(claim2.Result, Is.EqualTo(MarketClaimResult.Claimed));
        engine.FinalizePurchase("bob", claim2, "game", now.AddMinutes(5), enqueueItemDelivery: true, enqueueZenPayout: true);

        Assert.That(engine.PeekDeliveries("bob"), Has.Count.EqualTo(1));
        Assert.That(engine.PeekZenPayouts("alice").Sum(z => z.NetZen), Is.EqualTo(950));
        engine.CompleteDelivery(engine.PeekDeliveries("bob")[0].Id);
        Assert.That(engine.PeekDeliveries("bob"), Is.Empty);

        // Expiry returns l2 to alice
        Assert.That(engine.ExpireDueListings(now.AddHours(25)), Is.EqualTo(1));
        Assert.That(engine.PeekDeliveries("alice"), Has.Count.EqualTo(1));
        var audit = engine.Store.Read(d => d.AuditLog.ToList());
        Assert.That(audit.Any(a => a.Action == "Buy" && a.ActorLogin == "bob" && a.NetAmount == 950), Is.True);
        Assert.That(audit.Any(a => a.Action == "Expire" && a.ListingId == l2!.Id), Is.True);
    }

    private static MarketItemSnapshot SampleSword() => new()
    {
        Group = 0,
        Number = 19,
        Level = 13,
        Durability = 255,
        Skill = true,
        Luck = true,
        OptionLevel = 4,
        ExcellentOptions = 3,
        NameEn = "Sword of Archangel",
        NameVi = "Kiếm Thiên Sứ",
    };

    /// <summary>
    /// A listing taken back when its item could not leave the bag is gone, nothing is delivered for it, and
    /// taking it back twice changes nothing.
    /// </summary>
    [Test]
    public void AWithdrawnListingLeavesNoTraceAndNoDelivery()
    {
        var engine = this.CreateEngine();
        var listed = engine.CreateListing("seller1", "Knight", SampleSword(), MarketCurrency.WCoinC, 500, DateTime.UtcNow, out var listing);
        Assert.That(listed, Is.EqualTo(MarketListResult.Success));

        Assert.That(engine.WithdrawListing(listing!.Id), Is.True);
        Assert.That(engine.WithdrawListing(listing.Id), Is.False);
        Assert.That(engine.Store.Read(d => d.Listings.Count), Is.Zero);
        Assert.That(engine.PeekDeliveries("seller1"), Is.Empty, "the item never left the bag, so nothing is owed");
    }

    private MarketEngine CreateEngine(Action<MarketConfiguration>? configure = null)
    {
        var cfg = new MarketConfiguration
        {
            FileName = Path.Combine(this._tempDir, "hnmo-state.json"),
        };
        configure?.Invoke(cfg);
        return new MarketEngine(cfg, MarketStore.Open(cfg.FileName), enabled: true);
    }
}
