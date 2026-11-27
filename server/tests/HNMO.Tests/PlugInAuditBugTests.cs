// <copyright file="PlugInAuditBugTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Reflection;
using HNMO.AttributeSystem;
using HNMO.DataModel;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns.CombiningLeftovers;
using HNMO.GameLogic.PlugIns.ItemMarket;
using HNMO.GameLogic.PlugIns.ItemStacks;
using HNMO.GameLogic.PlugIns.JewelBank;
using HNMO.GameLogic.PlugIns.Marriage;
using HNMO.GameLogic.PlugIns.MountChain;
using HNMO.GameLogic.PlugIns.PartyOptions;
using HNMO.GameLogic.PlugIns.SeasonPlan;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using Microsoft.Extensions.Logging.Abstractions;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// Regression tests for PlugIn bugs discovered during the codebase audit.
/// </summary>
[TestFixture]
public class PlugInAuditBugTests
{
    private string _tempDir = string.Empty;

    /// <summary>Creates an isolated temporary folder per test.</summary>
    [SetUp]
    public void SetUp()
    {
        this._tempDir = Path.Combine(Path.GetTempPath(), $"hnmo-plugin-audit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this._tempDir);
    }

    /// <summary>Deletes temporary test files.</summary>
    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(this._tempDir))
        {
            Directory.Delete(this._tempDir, recursive: true);
        }
    }

    /// <summary>
    /// Depositing a stacked jewel into <see cref="JewelBankPlugIn"/> must credit all
    /// <see cref="Item.Durability"/> pieces (or partial capacity up to cap, keeping remainder in bag).
    /// </summary>
    [Test]
    public async ValueTask JewelBankDepositCreditsFullStackDurabilityAsync()
    {
        var stateFile = Path.Combine(this._tempDir, "state.json");
        var cfg = new JewelBankConfiguration
        {
            FileName = stateFile,
            SafeZoneOnly = false,
            DepositZenFee = 0,
            Jewels =
            [
                new JewelBankEntryConfig { Key = "bless", NameEn = "Bless", NameVi = "Bless", Group = 14, Number = 13, MaxCapacity = 30, Enabled = true },
            ],
        };

        var gameContext = GameContextTestHelper.CreateGameContext();
        var plugIn = new JewelBankPlugIn { Configuration = cfg };
        gameContext.PlugInManager.RegisterPlugInAtPlugInPoint<GameLogic.PlugIns.IPlayerStateChangedPlugIn>(plugIn);

        var blessDef = new BasicModel.ItemDefinition { Group = 14, Number = 13, Name = "Jewel of Bless", Width = 1, Height = 1, Durability = 255 };
        var player = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        player.Account!.LoginName = "stacker1";
        var stack25 = new BasicModel.Item { Definition = blessDef, Durability = 25 };
        await player.Inventory!.AddItemAsync(12, stack25).ConfigureAwait(false);

        await plugIn.DepositAsync(player, 12, depositAll: false).ConfigureAwait(false);

        async Task<int> BankAsync() => (await player.PersistenceContext.GetJewelBankBalancesAsync(player.Account.GetId()).ConfigureAwait(false)).GetValueOrDefault("bless");
        Assert.That(await BankAsync().ConfigureAwait(false), Is.EqualTo(25));
        Assert.That(player.Inventory.GetItem(12), Is.Null);

        // Now deposit a 10-stack when only 5 capacity remains -> balance = 30, bag stack = 5.
        var stack10 = new BasicModel.Item { Definition = blessDef, Durability = 10 };
        await player.Inventory.AddItemAsync(13, stack10).ConfigureAwait(false);
        await plugIn.DepositAsync(player, 13, depositAll: false).ConfigureAwait(false);

        Assert.That(await BankAsync().ConfigureAwait(false), Is.EqualTo(30));
        Assert.That(stack10.Durability, Is.EqualTo(5));
        Assert.That(player.Inventory.GetItem(13), Is.SameAs(stack10));
    }

    /// <summary>
    /// Failed <c>SaveProgressAsync</c> during <see cref="JewelBankPlugIn.ClearMasterSkillsAsync"/>
    /// must not record a ClearMaster event or leave an orphaned coin deduction receipt.
    /// </summary>
    [Test]
    public async ValueTask JewelBankClearMasterFailedSaveDoesNotRecordOrLeaveOrphanedReceiptAsync()
    {
        var stateFile = Path.Combine(this._tempDir, "state.json");
        var jbCfg = new JewelBankConfiguration { FileName = stateFile, AllowClearMasterSkills = true, ClearMasterZen = 1000, ClearMasterCoins = 50, ClearMasterCoinType = CashShopCoinType.WCoinC };
        var gameContext = this.CreateFailingSaveGameContext(out var rawProvider);
        var plugIn = new JewelBankPlugIn { Configuration = jbCfg };
        gameContext.PlugInManager.RegisterPlugInAtPlugInPoint<GameLogic.PlugIns.IPlayerStateChangedPlugIn>(plugIn);

        var player = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        player.Account!.LoginName = "clearfail";
        player.Account.WCoinC = 100;
        player.Money = 5000;
        var masterSkillDef = new BasicModel.Skill { Number = 300, Name = "Master", MasterDefinition = new BasicModel.MasterSkillDefinition() };
        player.SelectedCharacter!.LearnedSkills.Add(new BasicModel.SkillEntry { Skill = masterSkillDef, Level = 5 });

        await plugIn.ClearMasterSkillsAsync(player, fromPanel: true).ConfigureAwait(false);

        var engine = new JewelBankEngine(jbCfg, JewelBankStore.Open(stateFile), enabled: true);
        Assert.That(engine.Store.Read(d => d.Log.Count(l => string.Equals(l.Action, "ClearMaster", StringComparison.OrdinalIgnoreCase))), Is.EqualTo(0));
        Assert.That(player.Account.WCoinC, Is.EqualTo(100));
        Assert.That(player.Money, Is.EqualTo(5000));
        using var verifyCtx = rawProvider.CreateNewContext();
        var receipts = await verifyCtx.GetAsync<CashShopCoinGrant>().ConfigureAwait(false);
        Assert.That(receipts, Is.Empty);
    }

    /// <summary>
    /// <see cref="CombiningLeftoversEngine"/> and <see cref="CombiningLeftoversPlugIn"/> must
    /// count item stack durability, consume only the needed count (decrementing <see cref="Item.Durability"/>),
    /// and enforce active <see cref="SeasonPlanPlugIn"/> max box level caps.
    /// </summary>
    [Test]
    public async ValueTask CombiningLeftoversHandlesStackedIngredientsAndLiveSeasonCapAsync()
    {
        var stateFile = Path.Combine(this._tempDir, "state.json");
        var loCfg = new CombiningLeftoversConfiguration { FileName = stateFile, MaxBoxLevel = 5 };

        // PlugIn must count a single stacked Rena slot (Durability = 5) for ex-rena, decrement a stack of 10 by 5,
        // and honor live SeasonPlan MaxBoxLevel cap (Stage 1 MaxBoxLevel = 2 blocks box-2-to-3).
        var spCfg = new SeasonPlanConfiguration { FileName = stateFile };
        spCfg.Stages.First(s => s.Id == "stage-1-plus9").UnlockAtUtc = DateTime.UtcNow.AddDays(-1);
        var spPlugIn = new SeasonPlanPlugIn { Configuration = spCfg };
        var loPlugIn = new CombiningLeftoversPlugIn { Configuration = loCfg };

        var gameContext = GameContextTestHelper.CreateGameContext();
        gameContext.PlugInManager.RegisterPlugInAtPlugInPoint<GameLogic.PlugIns.IPlayerStateChangedPlugIn>(spPlugIn);
        gameContext.PlugInManager.RegisterPlugInAtPlugInPoint<GameLogic.PlugIns.IPlayerStateChangedPlugIn>(loPlugIn);

        var player = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        player.Account!.LoginName = "leftover1";
        player.Money = 10_000_000;

        // 1) Exchange 5 out of 7 stacked Renas in slot 12 -> slot 12 remains with Durability = 2.
        var renaDef = new BasicModel.ItemDefinition { Group = 14, Number = 21, Name = "Rena", Width = 1, Height = 1, Durability = 255 };
        var renaStack = new BasicModel.Item { Definition = renaDef, Level = 0, Durability = 7 };
        await player.Inventory!.AddItemAsync(12, renaStack).ConfigureAwait(false);
        await loPlugIn.ExchangeAsync(player, "ex-rena").ConfigureAwait(false);
        Assert.That(player.Inventory.GetItem(12), Is.SameAs(renaStack));
        Assert.That(renaStack.Durability, Is.EqualTo(2));

        // 2) Stage 1 caps boxes at Lv 2 -> combining 2->3 must be blocked!
        await player.Inventory.RemoveItemAsync(renaStack).ConfigureAwait(false);
        var box2Def = new BasicModel.ItemDefinition { Group = 14, Number = 11, Name = "Box of Luck", Width = 1, Height = 1, Durability = 255 };
        var jolDef = new BasicModel.ItemDefinition { Group = 14, Number = 16, Name = "Jewel of Life", Width = 1, Height = 1, Durability = 255 };
        var jocDef = new BasicModel.ItemDefinition { Group = 14, Number = 22, Name = "Jewel of Creation", Width = 1, Height = 1, Durability = 255 };
        var jol = new BasicModel.Item { Definition = jolDef, Durability = 5 };
        await player.Inventory.AddItemAsync(12, new BasicModel.Item { Definition = box2Def, Level = 9, Durability = 1 }).ConfigureAwait(false);
        await player.Inventory.AddItemAsync(13, new BasicModel.Item { Definition = box2Def, Level = 9, Durability = 1 }).ConfigureAwait(false);
        await player.Inventory.AddItemAsync(14, jol).ConfigureAwait(false);
        await player.Inventory.AddItemAsync(15, new BasicModel.Item { Definition = jocDef, Durability = 1 }).ConfigureAwait(false);

        await loPlugIn.CombineAsync(player, "box-2-to-3").ConfigureAwait(false);
        Assert.That(jol.Durability, Is.EqualTo(5));

        // Combine 1->2 uses Box+1 (level 8): place a 2-stack Box+1 + check jol stack decrements 5 -> 4 instead of deletion!
        await player.Inventory.RemoveItemAsync(player.Inventory.GetItem(12)!).ConfigureAwait(false);
        await player.Inventory.RemoveItemAsync(player.Inventory.GetItem(13)!).ConfigureAwait(false);
        await player.Inventory.AddItemAsync(12, new BasicModel.Item { Definition = box2Def, Level = 8, Durability = 2 }).ConfigureAwait(false);
        await loPlugIn.CombineAsync(player, "box-1-to-2").ConfigureAwait(false);
        Assert.That(player.Inventory.GetItem(14), Is.SameAs(jol));
        Assert.That(jol.Durability, Is.EqualTo(4));
    }

    /// <summary>
    /// Failed <c>SaveProgressAsync</c> in <see cref="MarketPlugIn.PayAccessAsync"/> and
    /// <see cref="ItemStackPlugIn.UnlockAsync"/> must remove the staged <see cref="CashShopCoinGrant"/> receipt.
    /// </summary>
    [Test]
    public async ValueTask FailedSaveInMarketAccessAndItemStackUnlockRemovesReceiptAsync()
    {
        var stateFile = Path.Combine(this._tempDir, "state.json");
        var gameContext = this.CreateFailingSaveGameContext(out var rawProvider);
        var marketPlugIn = new MarketPlugIn { Configuration = new MarketConfiguration { FileName = stateFile, AccessFee = 500 } };
        var stackPlugIn = new ItemStackPlugIn { Configuration = new ItemStackConfiguration { UnlockCoinType = CashShopCoinType.WCoinC } };

        var player = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        player.Account!.LoginName = "mktfail";
        player.Account.WCoinC = 2000;

        await marketPlugIn.PayAccessAsync(player, seller: true).ConfigureAwait(false);
        await stackPlugIn.UnlockAsync(player, "jewels").ConfigureAwait(false);

        Assert.That(player.Account.WCoinC, Is.EqualTo(2000));
        using var verifyCtx = rawProvider.CreateNewContext();
        var receipts = await verifyCtx.GetAsync<CashShopCoinGrant>().ConfigureAwait(false);
        Assert.That(receipts, Is.Empty);
    }

    /// <summary>
    /// <see cref="MarriagePlugIn"/> and <see cref="MountChainPlugIn"/> must use distinct magic effect IDs,
    /// and <see cref="MarriagePlugIn"/> must remove its couple buff when partners are no longer together.
    /// </summary>
    [Test]
    public async ValueTask MarriageAndMountChainDoNotCollideAndMarriageClearsStaleBuffAsync()
    {
        Assert.That(MarriagePlugIn.BuffEffectNumber, Is.Not.EqualTo(MountChainPlugIn.BuffEffectNumber));

        var stateFile = Path.Combine(this._tempDir, "state.json");
        var marryCfg = new MarriageConfiguration { FileName = stateFile, MinimumLevel = 1, RequireCeremonyLocation = false, CeremonyZenCost = 0 };
        var marryStore = MarriageStore.Open(stateFile);
        var marryEngine = new MarriageEngine(marryCfg, marryStore, enabled: true);
        var now = DateTime.UtcNow;
        Assert.That(marryEngine.Propose("SpouseA", "accA", 150, "SpouseB", 150, now), Is.EqualTo(MarriageActionResult.Success));
        Assert.That(marryEngine.AcceptCeremony("SpouseB", "accB", 150, 150, bothAtCeremonySpot: true, now.AddSeconds(5), out _), Is.EqualTo(MarriageActionResult.Success));

        var gameContext = GameContextTestHelper.CreateGameContext();
        var marryPlugIn = new MarriagePlugIn { Configuration = marryCfg };
        gameContext.PlugInManager.RegisterPlugInAtPlugInPoint<GameLogic.PlugIns.IPlayerStateChangedPlugIn>(marryPlugIn);

        var pA = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        pA.SelectedCharacter!.Name = "SpouseA";
        var pB = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        pB.SelectedCharacter!.Name = "SpouseB";

        // Same map & same party -> buff granted.
        var partyLogger = NullLogger<Party>.Instance;
        var party = new Party(new PartyManager(5, partyLogger), 5, partyLogger);
        await party.AddAsync(pA).ConfigureAwait(false);
        await party.AddAsync(pB).ConfigureAwait(false);

        await marryPlugIn.PlayerStateChangedAsync(pA, PlayerState.CharacterSelection, PlayerState.EnteredWorld).ConfigureAwait(false);
        Assert.That(pA.MagicEffectList.TryGetEffect(MarriagePlugIn.BuffEffectNumber, out _), Is.True);

        // Leave party -> buff must be disposed cleanly instead of lingering.
        await pA.Party!.KickPlayerAsync((byte)pA.Party.PartyList.TakeWhile(m => !ReferenceEquals(m, pA)).Count()).ConfigureAwait(false);
        await marryPlugIn.PlayerStateChangedAsync(pA, PlayerState.CharacterSelection, PlayerState.EnteredWorld).ConfigureAwait(false);
        Assert.That(pA.MagicEffectList.TryGetEffect(MarriagePlugIn.BuffEffectNumber, out _), Is.False);
    }

    /// <summary>
    /// Joining a passworded party via <c>/partypass</c> must succeed when
    /// <see cref="PartyOptionsConfiguration.AllowPasswordJoin"/> is true even if
    /// <see cref="PartyOptionsConfiguration.AllowAutoSpotParty"/> is false.
    /// </summary>
    [Test]
    public async ValueTask PartyPassWorksWhenAllowAutoSpotPartyIsFalseAsync()
    {
        var stateFile = Path.Combine(this._tempDir, "state.json");
        var cfg = new PartyOptionsConfiguration { AllowPasswordJoin = true, AllowAutoSpotParty = false };
        var engine = new PartyOptionsEngine(cfg, PartyOptionsStore.Open(stateFile), enabled: true);
        engine.SetAdvertisement(new PartyAdvertisement { LeaderName = "Leader", MapName = "Lorencia", SpotName = "Gate", Password = "secret", MinLevel = 1, MaxLevel = 400, MemberCount = 1, MaxSize = 5, UpdatedAtUtc = DateTime.UtcNow });

        var result = engine.PlanSpotJoin("Leader", "secret", playerLevel: 100, hasParty: false, out var ad);
        Assert.That(result, Is.EqualTo(PartyJoinCheckResult.Ok));
        Assert.That(ad?.LeaderName, Is.EqualTo("Leader"));
    }

    private IGameContext CreateFailingSaveGameContext(out InMemoryPersistenceContextProvider rawProvider)
    {
        var inner = new InMemoryPersistenceContextProvider();
        rawProvider = inner;
        var cfgCtx = inner.CreateNewContext();
        var gameConfig = cfgCtx.CreateNew<BasicModel.GameConfiguration>();
        var mapDef = cfgCtx.CreateNew<BasicModel.GameMapDefinition>();
        mapDef.Number = 0;
        mapDef.TerrainData = new byte[ushort.MaxValue + 3];
        gameConfig.Maps.Add(mapDef);
        gameConfig.MaximumPartySize = 5;
        gameConfig.RecoveryInterval = int.MaxValue;
        gameConfig.MaximumInventoryMoney = int.MaxValue;
        gameConfig.ItemDropDuration = TimeSpan.FromMinutes(1);
        gameConfig.MaximumLevel = 400;

        var wrappedProvider = DispatchProxy.Create<IPersistenceContextProvider, FailingSaveProviderProxy>();
        ((FailingSaveProviderProxy)(object)wrappedProvider).Inner = inner;

        var plugInManager = new PlugInManager(
            [new PlugInConfiguration { TypeId = new Guid("E90A72C3-0459-4323-B6D3-171F88D35542"), IsActive = true }],
            new NullLoggerFactory(),
            null,
            null);
        var mapInit = new MapInitializer(gameConfig, new NullLogger<MapInitializer>(), NullDropGenerator.Instance, null);
        var gc = new GameContext(gameConfig, wrappedProvider, mapInit, new NullLoggerFactory(), plugInManager, NullDropGenerator.Instance, new ConfigurationChangeMediator());
        mapInit.PlugInManager = gc.PlugInManager;
        mapInit.PathFinderPool = gc.PathFinderPool;
        return gc;
    }

    private class FailingSaveProviderProxy : DispatchProxy
    {
        public IPersistenceContextProvider Inner { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var result = targetMethod!.Invoke(this.Inner, args);
            if (targetMethod.Name == nameof(IPersistenceContextProvider.CreateNewPlayerContext) && result is IPlayerContext innerCtx)
            {
                var ctxProxy = DispatchProxy.Create<IPlayerContext, FailingSaveContextProxy>();
                ((FailingSaveContextProxy)(object)ctxProxy).Inner = innerCtx;
                return ctxProxy;
            }

            return result;
        }
    }

    private class FailingSaveContextProxy : DispatchProxy
    {
        public IPlayerContext Inner { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod!.Name == nameof(IContext.SaveChangesAsync))
            {
                return new ValueTask<bool>(false);
            }

            return targetMethod.Invoke(this.Inner, args);
        }
    }
}
