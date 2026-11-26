// <copyright file="AntiCheatPartySeasonAuditBugTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Threading;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Quests;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlayerActions.Quests;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.AntiCheat;
using HNMO.GameLogic.PlugIns.CatchupCap;
using HNMO.GameLogic.PlugIns.Economy;
using HNMO.GameLogic.PlugIns.SeasonPlan;
using HNMO.GameLogic.Seasons;
using HNMO.GameLogic.Views.World;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// Regression tests verifying fixes for AntiCheat speed/time computation, SeasonSharedAccounts concurrency,
/// Party master experience gating, Economy Jewel of Chaos counting, and Quest GensAttribution rewards.
/// </summary>
[TestFixture]
public class AntiCheatPartySeasonAuditBugTests
{
    /// <summary>
    /// <see cref="AntiCheatPlugIn.AttackCheatCheckAsync"/> must use the higher of
    /// <see cref="Stats.AttackSpeed"/> and <see cref="Stats.MagicSpeed"/>, and compute elapsed time via
    /// <see cref="TimeProvider.GetElapsedTime(long, long)"/> rather than <see cref="System.Diagnostics.Stopwatch.Frequency"/>.
    /// </summary>
    [Test]
    public async ValueTask AntiCheatUsesMagicSpeedAndTimeProviderElapsedAsync()
    {
        var clock = new TestTickTimeProvider(frequency: 1_000, utcNow: new DateTime(2026, 11, 25, 1, 0, 0, DateTimeKind.Utc));
        var cfg = new AntiCheatConfiguration
        {
            Enabled = true,
            BaseMinAttackIntervalMs = 400,
            MinimumAttackIntervalFloorMs = 80,
            LatencyToleranceFactor = 1.0,
            StrikesBeforeDisconnect = 3,
            EnableAttackFrequencyCheck = true,
            EnableHitHackAgilityCheck = false,
            FileName = Path.Combine(Path.GetTempPath(), $"hnmo-ac-test-{Guid.NewGuid():N}.json"),
        };

        try
        {
            var plugIn = new AntiCheatPlugIn(clock) { Configuration = cfg };
            var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
            player.Attributes!.AddElement(new SimpleElement(0, AggregateType.AddRaw), Stats.AttackSpeed);
            player.Attributes.AddElement(new SimpleElement(200, AggregateType.AddRaw), Stats.MagicSpeed);

            var args1 = new SpeedHackCheckEventArgs();
            await plugIn.AttackCheatCheckAsync(player, null, args1).ConfigureAwait(false);

            // Advance 350ms in clock ticks (350ms >= 310ms allowed at MagicSpeed 200, yet < 400ms at AttackSpeed 0 and ~0.035ms under Stopwatch.Frequency).
            clock.AdvanceMilliseconds(350);
            var args2 = new SpeedHackCheckEventArgs();
            await plugIn.AttackCheatCheckAsync(player, null, args2).ConfigureAwait(false);

            Assert.That(args2.Severity, Is.EqualTo(SpeedHackViolationSeverity.None));
        }
        finally
        {
            if (File.Exists(cfg.FileName))
            {
                File.Delete(cfg.FileName);
            }
        }
    }

    /// <summary>
    /// Concurrent <see cref="SeasonSharedAccounts.SyncCoinsAsync"/> calls for the same account must not
    /// double-apply in-flight coin deltas.
    /// </summary>
    [Test]
    public async Task SeasonSharedAccountsConcurrentSyncCoinsDoesNotDoubleApplyDeltaAsync()
    {
        SeasonSharedAccounts.Reset();
        var account = new Account { LoginName = "syncrace", WCoinC = 0 };
        var ctx = new Mock<IPlayerContext>();
        ctx.Setup(c => c.GetAccountByLoginNameAsync("syncrace", It.IsAny<CancellationToken>())).ReturnsAsync(account);
        ctx.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var store = new BlockingSharedAccountStore(new SharedAccount("syncrace", string.Empty, string.Empty, string.Empty, AccountState.Normal, null, null, new CoinBalances(100, 0, 0)));
        await SeasonSharedAccounts.MirrorAsync(store, () => ctx.Object, "syncrace").ConfigureAwait(false);

        account.WCoinC = 70; // -30 spend
        var firstSync = SeasonSharedAccounts.SyncCoinsAsync(store, account).AsTask();
        await store.FirstAddStarted.Task.ConfigureAwait(false);

        var secondSync = SeasonSharedAccounts.SyncCoinsAsync(store, account).AsTask();
        await Task.Delay(30).ConfigureAwait(false);
        store.ReleaseFirstAdd.SetResult();

        await Task.WhenAll(firstSync, secondSync).ConfigureAwait(false);

        Assert.That(store.Shared.Coins.WCoinC, Is.EqualTo(70));
        Assert.That(account.WCoinC, Is.EqualTo(70));
    }

    /// <summary>
    /// Party master experience must be gated by <see cref="CatchupCapPlugIn"/> effective max level and
    /// <see cref="SeasonPlanPlugIn"/> <see cref="SeasonFeatureGate.MasterLevels"/>.
    /// </summary>
    [Test]
    public async ValueTask PartyMasterExpRespectsCatchupCapAndSeasonPlanGateAsync()
    {
        var stateFile = Path.Combine(Path.GetTempPath(), $"hnmo-party-sp-{Guid.NewGuid():N}.json");
        try
        {
            var gameConfig = PlayerTestHelper.CreateGameConfiguration();
            gameConfig.MaximumLevel = 400;
            gameConfig.MaximumMasterLevel = 200;
            gameConfig.ExperienceRate = 1;
            gameConfig.MasterExperienceRate = 1;
            gameConfig.MinimumMonsterLevelForMasterExperience = 1;
            var mapInitializer = new MapInitializer(gameConfig, new NullLogger<MapInitializer>(), NullDropGenerator.Instance, null);
            var gameContext = new GameContext(gameConfig, new InMemoryPersistenceContextProvider(), mapInitializer, new NullLoggerFactory(), new PlugInManager(null, new NullLoggerFactory(), null, null), NullDropGenerator.Instance, new ConfigurationChangeMediator());
            mapInitializer.PlugInManager = gameContext.PlugInManager;
            mapInitializer.PathFinderPool = gameContext.PathFinderPool;

            var player = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
            player.CurrentMap!.Definition.ExpMultiplier = 1;

            var spCfg = new SeasonPlanConfiguration { FileName = stateFile };
            var spPlugIn = new SeasonPlanPlugIn { Configuration = spCfg };
            gameContext.PlugInManager.RegisterPlugInAtPlugInPoint<IPlayerStateChangedPlugIn>(spPlugIn);

            var capPlugIn = new CatchupCapPlugIn
            {
                Configuration = new CatchupCapConfiguration { FileName = stateFile, LevelCapEnabled = true, InitialLevelCap = 350, HardMaxLevelCap = 400 },
            };
            gameContext.PlugInManager.RegisterPlugInAtPlugInPoint<IPlayerStateChangedPlugIn>(capPlugIn);

            player.SelectedCharacter!.CharacterClass = new BasicModel.CharacterClass { Number = 2, Name = "Blade Master", IsMasterClass = true };
            player.Attributes![Stats.Level] = 400;
            player.Attributes.AddElement(new SimpleElement(400.0f, AggregateType.AddRaw), Stats.TotalLevel);
            player.Attributes[Stats.MasterLevel] = 1;
            player.Attributes.AddElement(new SimpleElement(1.0f, AggregateType.AddRaw), Stats.MasterExperienceRate);
            player.Attributes.AddElement(new SimpleElement(1.0f, AggregateType.AddRaw), Stats.ExperienceRate);
            player.SelectedCharacter.MasterExperience = 0;

            var monsterDef = new BasicModel.MonsterDefinition { Number = 1, Designation = "Bull", ObjectKind = NpcObjectKind.Monster };
            monsterDef.Attributes.Add(new BasicModel.MonsterAttribute { AttributeDefinition = Stats.Level, Value = 390 });
            monsterDef.Attributes.Add(new BasicModel.MonsterAttribute { AttributeDefinition = Stats.MaximumHealth, Value = 100 });
            monsterDef.Attributes.Add(new BasicModel.MonsterAttribute { AttributeDefinition = Stats.MinimumPhysBaseDmg, Value = 10 });
            var monster = new Monster(
                new BasicModel.MonsterSpawnArea { GameMap = player.CurrentMap!.Definition },
                monsterDef,
                player.CurrentMap!,
                NullDropGenerator.Instance,
                new Mock<INpcIntelligence>().Object,
                gameContext.PlugInManager,
                gameContext.PathFinderPool);
            monster.Initialize();

            var party = new Party(new PartyManager(5, new NullLogger<Party>()), 5, new NullLogger<Party>());
            await party.AddAsync(player).ConfigureAwait(false);
            var shares = await party.DistributeExperienceAfterKillAsync(monster, player).ConfigureAwait(false);

            Assert.That(player.SelectedCharacter.MasterExperience, Is.EqualTo(0));
            Assert.That(player.SelectedCharacter.Experience, Is.EqualTo(0));
            Assert.That(shares, Is.Not.Empty);
        }
        finally
        {
            if (File.Exists(stateFile))
            {
                File.Delete(stateFile);
            }
        }
    }

    /// <summary>
    /// <see cref="EconomyStore.CountJewelPieces"/> must count a single Jewel of Chaos (12:15) as 1 piece (or stack durability),
    /// while bundled Group 12 jewels still expand by <c>(Level + 1) * 10</c>.
    /// </summary>
    [Test]
    public void EconomyCountJewelPiecesDoesNotMultiplyJewelOfChaosByTen()
    {
        var chaosDef = new BasicModel.ItemDefinition { Group = 12, Number = 15, Name = "Jewel of Chaos", Durability = 1 };
        var chaos = new BasicModel.Item { Definition = chaosDef, Level = 0, Durability = 1 };

        var bundledBlessDef = new BasicModel.ItemDefinition { Group = 12, Number = 30, Name = "Bundled Jewel of Bless", Durability = 1 };
        var bundledBless = new BasicModel.Item { Definition = bundledBlessDef, Level = 2, Durability = 1 };

        Assert.That(EconomyStore.CountJewelPieces(chaos), Is.EqualTo(1));
        Assert.That(EconomyStore.CountJewelPieces(bundledBless), Is.EqualTo(30));
    }

    /// <summary>
    /// Completing a quest with <see cref="QuestRewardType.GensAttribution"/> must grant contribution points to the character's <see cref="GensMember"/>.
    /// </summary>
    [Test]
    public async Task QuestCompletionGrantsGensAttributionRewardAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.GensMember = new BasicModel.GensMember
        {
            CharacterId = player.SelectedCharacter!.Id,
            Gens = GensType.Duprian,
            Contribution = 50,
            Rank = 14,
        };

        var questDef = new BasicModel.QuestDefinition
        {
            Group = 18,
            Number = 5,
            Name = "Gens Test",
            QuestGiver = new BasicModel.MonsterDefinition { Number = 543, Designation = "Duprian" },
        };
        questDef.Rewards.Add(new BasicModel.QuestReward { RewardType = QuestRewardType.GensAttribution, Value = 25 });
        var state = new BasicModel.CharacterQuestState { Group = 18, ActiveQuest = questDef };
        Mock.Get(player.SelectedCharacter!).Setup(c => c.QuestStates).Returns(new List<CharacterQuestState> { state });
        player.OpenedNpc = new NonPlayerCharacter(new BasicModel.MonsterSpawnArea(), questDef.QuestGiver, player.CurrentMap!);

        await new QuestCompletionAction().CompleteQuestAsync(player, 18, 5).ConfigureAwait(false);

        Assert.That(player.GensMember.Contribution, Is.EqualTo(75));
    }

    private sealed class TestTickTimeProvider(long frequency, DateTime utcNow) : TimeProvider
    {
        private long _ticks = 1_000;

        public override long TimestampFrequency => frequency;

        public override long GetTimestamp() => this._ticks;

        public override DateTimeOffset GetUtcNow() => new(utcNow.AddMilliseconds(this._ticks), TimeSpan.Zero);

        public void AdvanceMilliseconds(long ms) => this._ticks += ms;
    }

    private sealed class BlockingSharedAccountStore(SharedAccount initial) : ISharedAccountStore
    {
        private int _calls;

        public SharedAccount Shared { get; private set; } = initial;

        public TaskCompletionSource FirstAddStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseFirstAdd { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask<SharedAccount?> FindAsync(string loginName, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<SharedAccount?>(this.Shared);

        public async ValueTask<CoinBalances?> AddCoinsAsync(string loginName, CoinBalances delta, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref this._calls) == 1)
            {
                this.FirstAddStarted.TrySetResult();
                await this.ReleaseFirstAdd.Task.ConfigureAwait(false);
            }

            var coins = this.Shared.Coins;
            var updated = new CoinBalances(
                Math.Max(0, coins.WCoinC + delta.WCoinC),
                Math.Max(0, coins.WCoinP + delta.WCoinP),
                Math.Max(0, coins.GoblinPoints + delta.GoblinPoints));
            this.Shared = this.Shared with { Coins = updated };
            return updated;
        }
    }
}
