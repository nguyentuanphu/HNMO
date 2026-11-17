// <copyright file="TopUpMilestoneEngineTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.TopUpMilestones;
using HNMO.GameLogic.Views;
using Moq;
using NUnit.Framework;

/// <summary>
/// Unit and plug-in tests for <see cref="TopUpMilestoneEngine"/> and <see cref="TopUpMilestonesPlugIn"/>.
/// </summary>
[TestFixture]
public class TopUpMilestoneEngineTest
{
    private string _file = null!;

    /// <summary>Creates a fresh temp path per test.</summary>
    [SetUp]
    public void SetUp()
    {
        this._file = Path.Combine(Path.GetTempPath(), $"hnmo-topup-milestones-{Guid.NewGuid():N}.json");
    }

    /// <summary>Removes temporary files.</summary>
    [TearDown]
    public void TearDown()
    {
        if (File.Exists(this._file))
        {
            File.Delete(this._file);
        }

        if (File.Exists(this._file + ".bak"))
        {
            File.Delete(this._file + ".bak");
        }
    }

    /// <summary>
    /// Verifies idempotent top-up VND accumulation, one-time milestone claims, and persistence across reload.
    /// </summary>
    [Test]
    public void TopUpAccumulatesOncePerReferenceAndStepsClaimOnce()
    {
        var now = new DateTime(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc);
        var config = new TopUpMilestonesConfiguration();
        var store = new TopUpMilestoneStore(this._file);
        var engine = new TopUpMilestoneEngine(config, store, enabled: true);

        var first = engine.RecordTopUp("Alice", 120_000, "bank:TX1", "Vietcombank", now);
        Assert.That(first.Recorded, Is.True);
        Assert.That(first.NewTotalVnd, Is.EqualTo(120_000));
        Assert.That(first.NewlyUnlockedSteps.Select(s => s.Id), Is.EqualTo(new[] { "step-100k" }));

        // Duplicate reference must be ignored cleanly.
        var dup = engine.RecordTopUp("ALICE", 120_000, "bank:TX1", "Vietcombank", now.AddMinutes(1));
        Assert.That(dup.Recorded, Is.False);
        Assert.That(dup.NewTotalVnd, Is.EqualTo(120_000));

        // Second top-up crosses 500k.
        var second = engine.RecordTopUp("alice", 400_000, "bank:TX2", "Techcombank", now.AddMinutes(5));
        Assert.That(second.Recorded, Is.True);
        Assert.That(second.NewTotalVnd, Is.EqualTo(520_000));
        Assert.That(second.NewlyUnlockedSteps.Select(s => s.Id), Is.EqualTo(new[] { "step-500k" }));

        // 1M step cannot be claimed yet.
        var tooLow = engine.Claim("alice", "step-1m", now.AddMinutes(6));
        Assert.That(tooLow.Result, Is.EqualTo(TopUpClaimResult.NotReached));

        // Claim 100k once.
        var claim = engine.Claim("alice", "step-100k", now.AddMinutes(7));
        Assert.That(claim.Result, Is.EqualTo(TopUpClaimResult.Ok));
        Assert.That(claim.CoinGrant?.Reference, Is.EqualTo("topup-milestone:alice:step-100k"));
        Assert.That(claim.CoinGrant?.Amount, Is.EqualTo(200));
        Assert.That(claim.OwedReward?.Items, Has.Count.EqualTo(1));

        // Second claim of the same step is refused.
        var claimAgain = engine.Claim("alice", "step-100k", now.AddMinutes(8));
        Assert.That(claimAgain.Result, Is.EqualTo(TopUpClaimResult.AlreadyClaimed));

        lock (store.Lock)
        {
            store.SaveIfChanged();
        }

        // Simulate restart with fresh store & engine.
        var restarted = new TopUpMilestoneEngine(config, new TopUpMilestoneStore(this._file), enabled: true);
        var snap = restarted.SnapshotFor("alice");
        Assert.That(snap.TotalVnd, Is.EqualTo(520_000));
        Assert.That(snap.NextThresholdVnd, Is.EqualTo(1_000_000));
        Assert.That(snap.Steps.Single(s => s.Step.Id == "step-100k").Claimed, Is.True);
        Assert.That(snap.Steps.Single(s => s.Step.Id == "step-500k").Claimable, Is.True);
    }

    /// <summary>
    /// Verifies that claims are blocked when disabled, even if cumulative VND exceeds steps.
    /// </summary>
    [Test]
    public void DisabledEngineBlocksClaim()
    {
        var store = new TopUpMilestoneStore();
        var engine = new TopUpMilestoneEngine(new TopUpMilestonesConfiguration(), store, enabled: false);
        engine.RecordTopUp("bob", 500_000, "bank:B1", null, DateTime.UtcNow);
        var claim = engine.Claim("bob", "step-100k", DateTime.UtcNow);
        Assert.That(claim.Result, Is.EqualTo(TopUpClaimResult.Disabled));
    }

    /// <summary>
    /// Verifies owed item queue take and return when the bag is full.
    /// </summary>
    [Test]
    public void OwedRewardsQueueSupportsReturnOnFullBag()
    {
        var store = new TopUpMilestoneStore();
        var engine = new TopUpMilestoneEngine(new TopUpMilestonesConfiguration(), store, enabled: true);
        engine.RecordTopUp("carl", 100_000, "bank:C1", null, DateTime.UtcNow);
        engine.Claim("carl", "step-100k", DateTime.UtcNow);

        var taken = engine.TakeOwed("carl");
        Assert.That(taken, Has.Count.EqualTo(1));
        Assert.That(engine.SnapshotFor("carl").Owed, Has.Count.EqualTo(0));

        engine.ReturnOwed("carl", taken);
        Assert.That(engine.SnapshotFor("carl").Owed, Has.Count.EqualTo(1));
    }

    /// <summary>
    /// Verifies <see cref="TopUpMilestonesPlugIn"/> sends state and claims for an online player.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PlugInSendsStateAndClaimsForOnlinePlayerAsync()
    {
        var context = GameContextTestHelper.CreateGameContext();
        var plugIn = new TopUpMilestonesPlugIn
        {
            Configuration = new TopUpMilestonesConfiguration { FileName = this._file },
        };
        var player = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        player.Account ??= new HNMO.Persistence.BasicModel.Account();
        player.Account.LoginName = "alice";
        player.Account.IsBot = false;
        var viewPlugIn = player.ViewPlugIns.GetPlugIn<IHnmoTopUpMilestonesViewPlugIn>();
        Assert.That(viewPlugIn, Is.Not.Null);

        var engine = plugIn.GetEngine();
        engine.RecordTopUp(player.Account!.LoginName, 150_000, "bank:LIVE1", "manual", DateTime.UtcNow);

        await plugIn.ClaimAsync(player, "step-100k").ConfigureAwait(false);

        Mock.Get(viewPlugIn!).Verify(v => v.ShowTopUpMilestonesAsync(It.Is<string>(j => j.Contains("\"claimed\":true")), true), Times.Once);
    }
}
