// <copyright file="S6EventsExtendedTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.MossMerchant;
using HNMO.GameLogic.PlugIns.PeriodicTasks;
using HNMO.GameLogic.PlugIns.S6Events;
using HNMO.GameLogic.PlugIns.Timetable;
using HNMO.Persistence.InMemory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// Unit tests for Q6 Season 6 events: Loren Deep, Mining System, Mu Rummy, XMas Santa buffs/gifts, and Moss weighted Exc selection.
/// </summary>
[TestFixture]
public class S6EventsExtendedTests
{
    private string _statePath = string.Empty;

    /// <summary>Creates temp state path per test.</summary>
    [SetUp]
    public void SetUp()
    {
        this._statePath = Path.Combine(Path.GetTempPath(), $"hnmo-s6ext-{Guid.NewGuid():N}.json");
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

    /// <summary>Verifies Loren Deep wave definitions and Timetable publication.</summary>
    [Test]
    public void LorenDeepWavesAndTimetableAreConfigured()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LorenDeepEngine.SelectAssaultType(50, 50, 0.25), Is.EqualTo(LorenDeepEngine.AssaultType.CommonAssault));
            Assert.That(LorenDeepEngine.SelectAssaultType(50, 50, 0.75), Is.EqualTo(LorenDeepEngine.AssaultType.SpecialRaid));
            Assert.That(LorenDeepEngine.GetWaveSpawns(LorenDeepEngine.AssaultType.SpecialRaid, 2).Any(s => s.MonsterNumber == 302), Is.True);
            Assert.That(LorenDeepEngine.GetWaveSpawns(LorenDeepEngine.AssaultType.SpecialRaid, 3).Any(s => s.MonsterNumber == 295), Is.True);
        });

        var gameConfig = PlayerTestHelper.CreateGameConfiguration();
        var mapInit = new MapInitializer(gameConfig, new NullLogger<MapInitializer>(), NullDropGenerator.Instance, null);
        var gameContext = new GameContext(gameConfig, new InMemoryPersistenceContextProvider(), mapInit, new NullLoggerFactory(), new HNMO.PlugIns.PlugInManager(null, new NullLoggerFactory(), null, null), NullDropGenerator.Instance, new ConfigurationChangeMediator());
        gameContext.PlugInManager.RegisterPlugIn<IPeriodicTaskPlugIn, S6EventsPlugIn>();
        gameContext.PlugInManager.ActivatePlugIn<S6EventsPlugIn>();
        var plugIn = S6EventsPlugIn.Get(gameContext)!;
        plugIn.Configuration = new S6EventsConfiguration { Enabled = true, LmsEnabled = false, LorenDeepEnabled = true, FileName = this._statePath };

        var entries = TimetableSourceCollector.CollectFromContext(
            gameContext,
            new TimetableConfiguration { ShowInvasions = true, ShowMiniGames = false, ShowWorldBosses = false },
            DateTime.UtcNow);
        Assert.That(entries.Any(e => e.Id == "event-lorendeep"), Is.True);
    }

    /// <summary>Verifies Mining System payouts, Bless repair values, and command durability handling.</summary>
    [Test]
    public async ValueTask MiningSystemConsumesPickDurabilityRewardsBlessAndRepairsAsync()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MiningSystemEngine.GetDurabilityCost(MiningTwinkleType.Small), Is.EqualTo(2));
            Assert.That(MiningSystemEngine.GetDurabilityCost(MiningTwinkleType.Medium), Is.EqualTo(4));
            Assert.That(MiningSystemEngine.GetDurabilityCost(MiningTwinkleType.Large), Is.EqualTo(8));
            Assert.That(MiningSystemEngine.GetRewardBlessTotal(MiningTwinkleType.Large, 5, isPandoraJackpot: true), Is.EqualTo(1000));
            Assert.That(MiningSystemEngine.GetRepairAmount(14, 13, 0), Is.EqualTo(4));
            Assert.That(MiningSystemEngine.GetRepairAmount(12, 30, 1), Is.EqualTo(80));
            var items33 = MiningSystemEngine.BuildBlessPayoutItems(33);
            Assert.That(items33.Sum(r => r.ItemLevel == 2 && r.ItemNumber == 30 ? 30 * r.ItemCount : r.ItemCount), Is.EqualTo(33));
        });

        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Account!.LoginName = "miner1";
        var ctx = player.PersistenceContext;
        var cfg = player.GameContext.Configuration;
        var pickDef = new ItemDefinition { Group = 0, Number = 41, Width = 2, Height = 2, Durability = 255, Name = "Pandora Pick" };
        var blessDef = new ItemDefinition { Group = 14, Number = 13, Width = 1, Height = 1, Durability = 1, Name = "Jewel of Bless" };
        var bundleDef = new ItemDefinition { Group = 12, Number = 30, Width = 1, Height = 1, Durability = 1, MaximumItemLevel = 2, Name = "Packed Bless" };
        cfg.Items.Add(pickDef);
        cfg.Items.Add(blessDef);
        cfg.Items.Add(bundleDef);

        var pick = ctx.CreateNew<Item>();
        pick.Definition = pickDef;
        pick.Durability = 10;
        await player.Inventory!.AddItemAsync(12, pick).ConfigureAwait(false);

        var bless = ctx.CreateNew<Item>();
        bless.Definition = blessDef;
        bless.Durability = 1;
        await player.Inventory.AddItemAsync(14, bless).ConfigureAwait(false);

        player.GameContext.PlugInManager.RegisterPlugIn<IPeriodicTaskPlugIn, S6EventsPlugIn>();
        player.GameContext.PlugInManager.ActivatePlugIn<S6EventsPlugIn>();
        var plugIn = S6EventsPlugIn.Get(player.GameContext)!;
        plugIn.Configuration = new S6EventsConfiguration
        {
            Enabled = true,
            MiningEnabled = true,
            MiningChanceDenominator = 1_000_000,
            MiningSmallRates = [1_000_000, 1_000_000, 1_000_000, 1_000_000, 1_000_000],
            MiningPandoraRates = [0, 0, 0],
            FileName = this._statePath,
        };

        var mined = await plugIn.HandleMineCommandAsync(player, "/mine small 1").ConfigureAwait(false);
        Assert.Multiple(() =>
        {
            Assert.That(mined, Is.True);
            Assert.That(pick.Durability, Is.EqualTo(8));
        });

        var repaired = await plugIn.HandleMineCommandAsync(player, "/mine repair").ConfigureAwait(false);
        Assert.Multiple(() =>
        {
            Assert.That(repaired, Is.True);
            Assert.That(pick.Durability, Is.EqualTo(12));
        });
    }

    /// <summary>Verifies Mu Rummy meld scoring, combining Card Pieces into Card Deck, and playing via /rummy.</summary>
    [Test]
    public async ValueTask MuRummyCombinesPiecesScoresMeldsAndRewardsAsync()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MuRummyEngine.EvaluateMeldScore(new(1, 2), new(1, 3), new(1, 4)), Is.EqualTo(50));
            Assert.That(MuRummyEngine.EvaluateMeldScore(new(1, 5), new(2, 5), new(3, 5)), Is.EqualTo(60));
            Assert.That(MuRummyEngine.EvaluateMeldScore(new(1, 1), new(2, 3), new(3, 7)), Is.Zero);
            Assert.That(MuRummyEngine.ResolveReward(400, 250, 400, 0.05).ItemNumber, Is.EqualTo(31));
        });

        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Account!.LoginName = "rummy1";
        player.SelectedCharacter!.Name = "rummy1";
        var ctx = player.PersistenceContext;
        var cfg = player.GameContext.Configuration;
        cfg.MaximumInventoryMoney = int.MaxValue;
        var pieceDef = new ItemDefinition { Group = 14, Number = 215, Width = 1, Height = 1, Durability = 24, Name = "Card Piece" };
        var deckDef = new ItemDefinition { Group = 14, Number = 216, Width = 1, Height = 1, Durability = 1, Name = "Card Deck" };
        cfg.Items.Add(pieceDef);
        cfg.Items.Add(deckDef);

        var stack = ctx.CreateNew<Item>();
        stack.Definition = pieceDef;
        stack.Durability = 24;
        await player.Inventory!.AddItemAsync(12, stack).ConfigureAwait(false);

        player.GameContext.PlugInManager.RegisterPlugIn<IPeriodicTaskPlugIn, S6EventsPlugIn>();
        player.GameContext.PlugInManager.ActivatePlugIn<S6EventsPlugIn>();
        var plugIn = S6EventsPlugIn.Get(player.GameContext)!;
        plugIn.Configuration = new S6EventsConfiguration { Enabled = true, MuRummyEnabled = true, FileName = this._statePath };

        var combined = await plugIn.HandleRummyCommandAsync(player, "/rummy combine").ConfigureAwait(false);
        Assert.That(combined, Is.True);
        Assert.That(S6EventsEngine.CountStackedItems(player.Inventory, 14, 216), Is.EqualTo(1));

        var started = await plugIn.HandleRummyCommandAsync(player, "/rummy start").ConfigureAwait(false);
        Assert.That(started, Is.True);

        var finished = await plugIn.HandleRummyCommandAsync(player, "/rummy finish").ConfigureAwait(false);
        Assert.That(finished, Is.True);
        Assert.That(player.Money, Is.EqualTo(500_000));
    }

    /// <summary>Verifies XMas Santa Village buff bonuses, timed effect application, and Snowman gift cooldowns.</summary>
    [Test]
    public async ValueTask XMasSantaBuffsAndSnowmanGiftCooldownWorkAsync()
    {
        Assert.Multiple(() =>
        {
            Assert.That(XMasSantaEngine.ComputeBlessingBonuses(90), Is.EqualTo((75f, 68f)));
            Assert.That(XMasSantaEngine.ComputeBlessingBonuses(300), Is.EqualTo((105f, 86f)));
        });

        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Account!.LoginName = "xmas1";
        player.SelectedCharacter!.Name = "xmas1";
        player.GameContext.Configuration.MaximumInventoryMoney = int.MaxValue;
        player.Attributes![Stats.Level] = 100;
        player.GameContext.PlugInManager.RegisterPlugIn<IPeriodicTaskPlugIn, S6EventsPlugIn>();
        player.GameContext.PlugInManager.ActivatePlugIn<S6EventsPlugIn>();
        var plugIn = S6EventsPlugIn.Get(player.GameContext)!;
        plugIn.Configuration = new S6EventsConfiguration { Enabled = true, XMasEnabled = true, XMasMinReset = 0, XMasGiftCooldownMinutes = 60, FileName = this._statePath };

        var firstClaim = await plugIn.HandleXMasCommandAsync(player).ConfigureAwait(false);
        var secondClaim = await plugIn.HandleXMasCommandAsync(player).ConfigureAwait(false);
        await plugIn.ApplySantaBuffAsync(player, plugIn.Configuration, 470).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(firstClaim, Is.True);
            Assert.That(secondClaim, Is.False);
            Assert.That(player.Money, Is.GreaterThanOrEqualTo(30_000));
            Assert.That(player.MagicEffectList.ContainsEffect((short)XMasBuffKind.SantaHealing), Is.True);
            Assert.That(player.MagicEffectList.ContainsEffect((short)XMasBuffKind.BlessingOfChristmas), Is.True);
        });
    }

    /// <summary>Verifies <see cref="MossGambleCatalog.RollReward"/> respects the weighted Excellent option bit flag.</summary>
    [Test]
    public void MossGambleCatalogWeightedExcellentSelectionWorks()
    {
        var ctx = new InMemoryPersistenceContextProvider().CreateNewContext();
        var cfg = ctx.CreateNew<GameConfiguration>();
        var itemDef = ctx.CreateNew<ItemDefinition>();
        itemDef.Group = 0;
        itemDef.Number = 1;
        itemDef.Width = 1;
        itemDef.Height = 2;
        itemDef.Durability = 20;
        itemDef.Name = "Short Sword";
        var excSet = ctx.CreateNew<ItemOptionDefinition>();
        excSet.Name = "ExcTest";
        for (var i = 0; i < 6; i++)
        {
            var opt = ctx.CreateNew<IncreasableItemOption>();
            opt.Number = i;
            opt.OptionType = ItemOptionTypes.Excellent;
            excSet.PossibleOptions.Add(opt);
        }

        itemDef.PossibleItemOptions.Add(excSet);
        cfg.Items.Add(itemDef);

        var alwaysRollExc = new Mock<IRandomizer>();
        alwaysRollExc.Setup(r => r.NextInt(It.IsAny<int>(), It.IsAny<int>())).Returns<int, int>((min, max) => min);

        var rolled = MossGambleCatalog.RollReward(cfg, 71, alwaysRollExc.Object, useWeightedExcellentSelection: true);
        Assert.That(rolled, Is.Not.Null);
        Assert.That(rolled!.ItemOptions.Count, Is.GreaterThanOrEqualTo(2));
    }
}
