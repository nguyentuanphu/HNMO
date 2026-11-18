// <copyright file="S6EventsTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.PeriodicTasks;
using HNMO.GameLogic.PlugIns.S6Events;
using HNMO.GameLogic.PlugIns.Timetable;
using HNMO.Persistence.InMemory;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Unit tests for <see cref="S6EventsEngine"/>, <see cref="S6EventsStore"/>, and <see cref="S6EventsPlugIn"/>.
/// </summary>
[TestFixture]
public class S6EventsTests
{
    private string _statePath = string.Empty;

    /// <summary>Creates a fresh temp state file path per test.</summary>
    [SetUp]
    public void SetUp()
    {
        this._statePath = Path.Combine(Path.GetTempPath(), $"hnmo-s6e-{Guid.NewGuid():N}.json");
    }

    /// <summary>Deletes temp state file after test.</summary>
    [TearDown]
    public void TearDown()
    {
        if (File.Exists(this._statePath))
        {
            File.Delete(this._statePath);
        }
    }

    /// <summary>Verifies tier selection and weighted reward picking rules.</summary>
    [Test]
    public void SelectsAffordableTierAndWeightedReward()
    {
        var tiers = S6EventsConfiguration.CreateDefaultCherryBlossomTiers();

        Assert.That(S6EventsEngine.SelectAffordableTier(tiers, 9)?.RequiredCount, Is.Null);
        Assert.That(S6EventsEngine.SelectAffordableTier(tiers, 55)?.RequiredCount, Is.EqualTo(50));
        Assert.That(S6EventsEngine.SelectAffordableTier(tiers, 55, requestedCount: 10)?.RequiredCount, Is.EqualTo(10));

        var rewards = tiers[0].Rewards;
        Assert.That(S6EventsEngine.PickWeightedReward(rewards, 0.1)?.ItemNumber, Is.EqualTo(85));
        Assert.That(S6EventsEngine.PickWeightedReward(rewards, 0.9)?.ItemNumber, Is.EqualTo(86));
    }

    /// <summary>Verifies schedule slot matching and single-claim deduplication.</summary>
    [Test]
    public void ResolvesScheduleSlotsAndDeduplicatesStoreClaims()
    {
        var tz = TimetableEngine.ResolveTimeZone("Australia/Sydney");
        var localTarget = new DateTime(2026, 11, 18, 20, 30, 0, DateTimeKind.Unspecified);
        var utcTarget = TimeZoneInfo.ConvertTimeToUtc(localTarget, tz);

        var slotKey = S6EventsEngine.ResolveDueScheduleSlot(["20:30"], "Australia/Sydney", utcTarget);
        Assert.That(slotKey, Is.EqualTo("2026-11-18 20:30"));

        var store = S6EventsStore.Open(this._statePath);
        Assert.That(store.TryClaimScheduleSlot(slotKey!), Is.True);
        Assert.That(store.TryClaimScheduleSlot(slotKey!), Is.False);
    }

    /// <summary>Verifies Cherry Blossom &amp; Lucky Coin stacked item consumption and queued reward fallback.</summary>
    [Test]
    public async ValueTask ConsumesStackedBranchesAndDeliversExchangeRewardsAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.GameContext.Configuration.MaximumInventoryMoney = int.MaxValue;
        player.Account!.LoginName = "s6acct";
        var branchDef = new ItemDefinition { Group = 14, Number = 90, Durability = 255, Width = 1, Height = 2, Name = "Golden Cherry Blossom Branch" };
        var stack1 = player.PersistenceContext.CreateNew<Item>();
        stack1.Definition = branchDef;
        stack1.Durability = 7;
        await player.Inventory!.AddItemAsync(12, stack1).ConfigureAwait(false);

        var stack2 = player.PersistenceContext.CreateNew<Item>();
        stack2.Definition = branchDef;
        stack2.Durability = 6;
        await player.Inventory.AddItemAsync(14, stack2).ConfigureAwait(false);

        Assert.That(S6EventsEngine.CountStackedItems(player.Inventory, 14, 90), Is.EqualTo(13));

        var cfg = new S6EventsConfiguration
        {
            Enabled = true,
            FileName = this._statePath,
            CherryBlossomEnabled = true,
        };
        player.GameContext.PlugInManager.RegisterPlugIn<IPeriodicTaskPlugIn, S6EventsPlugIn>();
        player.GameContext.PlugInManager.ActivatePlugIn<S6EventsPlugIn>();
        var plugin = S6EventsPlugIn.Get(player.GameContext)!;
        plugin.Configuration = cfg;

        var exchanged = await plugin.HandleExchangeCommandAsync(player, isCherryBlossom: true, requestedCount: 10).ConfigureAwait(false);
        Assert.That(exchanged, Is.True);
        Assert.That(S6EventsEngine.CountStackedItems(player.Inventory, 14, 90), Is.EqualTo(3));
        Assert.That(player.Money, Is.EqualTo(500_000));
    }

    /// <summary>Verifies LMS registration via /lms, elimination tracking, and Timetable integration.</summary>
    [Test]
    public async ValueTask RunsLastManStandingRegistrationEliminationAndTimetableExportAsync()
    {
        var gameConfig = PlayerTestHelper.CreateGameConfiguration();
        gameConfig.MaximumInventoryMoney = int.MaxValue;
        var mapInit = new MapInitializer(gameConfig, new NullLogger<MapInitializer>(), NullDropGenerator.Instance, null);
        var gameContext = new GameContext(gameConfig, new InMemoryPersistenceContextProvider(), mapInit, new NullLoggerFactory(), new HNMO.PlugIns.PlugInManager(null, new NullLoggerFactory(), null, null), NullDropGenerator.Instance, new ConfigurationChangeMediator());
        mapInit.PlugInManager = gameContext.PlugInManager;
        mapInit.PathFinderPool = gameContext.PathFinderPool;

        var p1 = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        p1.Account!.LoginName = "fighter1";
        p1.SelectedCharacter!.Name = "FighterOne";
        p1.Attributes![Stats.Level] = 100;

        var p2 = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        p2.Account!.LoginName = "fighter2";
        p2.SelectedCharacter!.Name = "FighterTwo";
        p2.Attributes![Stats.Level] = 100;

        var cfg = new S6EventsConfiguration
        {
            Enabled = true,
            FileName = this._statePath,
            LmsEnabled = true,
            LmsMinPlayers = 2,
            LmsMinLevel = 50,
            LmsRewardCoins = 0, // skip coin DB call in minimal InMemory test context; Zen delivered immediately/queued
            LmsRewardZen = 2_000_000,
            LmsRewardItemGroup = -1,
        };
        gameContext.PlugInManager.RegisterPlugIn<IPeriodicTaskPlugIn, S6EventsPlugIn>();
        gameContext.PlugInManager.ActivatePlugIn<S6EventsPlugIn>();
        var active = S6EventsPlugIn.Get(gameContext)!;
        active.Configuration = cfg;

        var triggerStatus = await S6EventsPlugIn.TriggerLmsNowAsync([gameContext]).ConfigureAwait(false);
        Assert.That(triggerStatus, Is.EqualTo("Ok"));
        Assert.That(active.CurrentLmsPhase, Is.EqualTo(S6EventsPlugIn.LmsPhase.Registration));

        var ttSources = TimetableSourceCollector.CollectFromContext(gameContext, new TimetableConfiguration(), DateTime.UtcNow);
        var lmsTimetable = ttSources.FirstOrDefault(s => s.Id == "event-lms");
        Assert.That(lmsTimetable, Is.Not.Null);
        Assert.That(lmsTimetable!.IsLiveNow, Is.True);

        await active.HandleLmsCommandAsync(p1).ConfigureAwait(false);
        await active.HandleLmsCommandAsync(p2).ConfigureAwait(false);
        Assert.That(active.CurrentContestantCount, Is.EqualTo(2));

        await active.BeginLmsFightOrCancelAsync(gameContext, cfg).ConfigureAwait(false);
        Assert.That(active.CurrentLmsPhase, Is.EqualTo(S6EventsPlugIn.LmsPhase.Fighting));

        await active.AttackableGotKilledAsync(p2, p1).ConfigureAwait(false);
        Assert.That(active.CurrentLmsPhase, Is.EqualTo(S6EventsPlugIn.LmsPhase.Idle));
        Assert.That(p1.Money, Is.EqualTo(2_000_000));

        var history = S6EventsStore.Open(this._statePath).Snapshot().LmsHistory;
        Assert.That(history, Has.Count.EqualTo(1));
        Assert.That(history[0].CharacterName, Is.EqualTo("FighterOne"));
        Assert.That(history[0].Kills, Is.EqualTo(1));
    }
}
