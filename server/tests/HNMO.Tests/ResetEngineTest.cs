// <copyright file="ResetEngineTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns.ResetOptions;

/// <summary>
/// Tests for HNMO's reset options: tiers, costs (zen, coins, items), daily limits on the Sydney day, VIP advantages, the
/// relife with its permanent bonus, the monthly ranking, the store, and resets done for a player in the game and for an
/// offline leveling character (delegated reset).
/// </summary>
[TestFixture]
public class ResetEngineTest
{
    // 2026-11-15 13:00 UTC = 2026-11-16 00:00 Sydney (daylight saving, UTC+11).
    private static readonly DateTime SydneyMidnight = new(2026, 11, 15, 13, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// The first tier which covers the reset count counts; none covers a count after the last tier.
    /// </summary>
    [Test]
    public void TheTierOfAResetCountIsTheFirstWhichCoversIt()
    {
        var engine = Engine(Configuration());
        Assert.That(engine.TierFor(0)!.RequiredLevel, Is.EqualTo(300));
        Assert.That(engine.TierFor(9)!.RequiredLevel, Is.EqualTo(300));
        Assert.That(engine.TierFor(10)!.RequiredLevel, Is.EqualTo(350));
        Assert.That(engine.TierFor(20), Is.Null);
        Assert.That(engine.PlanReset(Subject(level: 400, resets: 20), SydneyMidnight, ResetSource.Game).Result, Is.EqualTo(ResetResult.NoTier));
    }

    /// <summary>
    /// The plan has the tier's level, zen (times the reset number when set), coins and points; it checks the level,
    /// the zen, the coins and the items in that order.
    /// </summary>
    [Test]
    public void ThePlanHasTheTiersCostsAndChecksThem()
    {
        var configuration = Configuration();
        configuration.Tiers[0].ZenTimesResetNumber = true;
        configuration.Tiers[0].Items.Add(new ResetItemCost { Group = 14, Number = 13, Count = 2 });
        var engine = Engine(configuration);

        var plan = engine.PlanReset(Subject(level: 299, resets: 4), SydneyMidnight, ResetSource.Game);
        Assert.That(plan.Result, Is.EqualTo(ResetResult.LevelTooLow));
        Assert.That((plan.RequiredLevel, plan.Zen, plan.ResetsAfter), Is.EqualTo((300, 5_000L, 5)));

        Assert.That(engine.PlanReset(Subject(level: 300, resets: 4, zen: 4_999), SydneyMidnight, ResetSource.Game).Result, Is.EqualTo(ResetResult.NotEnoughZen));
        configuration.Tiers[0].Coins = 10;
        Assert.That(engine.PlanReset(Subject(level: 300, resets: 4, coins: 9), SydneyMidnight, ResetSource.Game).Result, Is.EqualTo(ResetResult.NotEnoughCoins));
        var missing = engine.PlanReset(Subject(level: 300, resets: 4, items: 1), SydneyMidnight, ResetSource.Game);
        Assert.That(missing.Result, Is.EqualTo(ResetResult.NotEnoughItems));
        Assert.That(missing.MissingItem!.Count, Is.EqualTo(2));
        var ok = engine.PlanReset(Subject(level: 300, resets: 4, items: 2), SydneyMidnight, ResetSource.Game);
        Assert.That(ok.Result, Is.EqualTo(ResetResult.Ok));
        Assert.That(ok.Coins.Single(), Is.EqualTo(new ResetCoinCost(CashShopCoinType.GoblinPoints, 10)));
    }

    /// <summary>
    /// In the total mode, the free points are the points of every reset done (each by its tier) and the relifes' bonus;
    /// in the add mode only the reset's points are added; in the keep mode none.
    /// </summary>
    [Test]
    public void ThePointsFollowTheMode()
    {
        var configuration = Configuration();
        var engine = Engine(configuration);

        // Resets 1-10 give 100 each, 11 gives 200: 10 x 100 + 200.
        Assert.That(engine.PlanReset(Subject(level: 400, resets: 10), SydneyMidnight, ResetSource.Game).Points, Is.EqualTo(1_200));
        engine.Store.Data.Characters["c1"] = new ResetCharacterRecord { Relifes = 2 };
        Assert.That(engine.PlanReset(Subject(level: 400, resets: 10), SydneyMidnight, ResetSource.Game).Points, Is.EqualTo(1_200 + (2 * 1_000)));

        configuration.Tiers[1].PointsMode = ResetPointsMode.Add;
        Assert.That(engine.PlanReset(Subject(level: 400, resets: 10), SydneyMidnight, ResetSource.Game).Points, Is.EqualTo(200));
        configuration.Tiers[1].PointsMode = ResetPointsMode.Keep;
        Assert.That(engine.PlanReset(Subject(level: 400, resets: 10), SydneyMidnight, ResetSource.Game).Points, Is.EqualTo(0));
    }

    /// <summary>
    /// The daily limit counts the resets of the Sydney day (every source); a new day starts at midnight Sydney time.
    /// </summary>
    [Test]
    public void TheDailyLimitCountsTheSydneyDay()
    {
        var configuration = Configuration();
        configuration.Tiers[0].DailyLimit = 2;
        var engine = Engine(configuration);
        var subject = Subject(level: 400, resets: 0);
        var beforeMidnight = SydneyMidnight.AddMinutes(-30);
        for (var i = 0; i < 2; i++)
        {
            var plan = engine.PlanReset(subject, beforeMidnight, ResetSource.Game);
            Assert.That(plan.Result, Is.EqualTo(ResetResult.Ok));
            engine.Record(subject, plan, beforeMidnight, i == 0 ? ResetSource.Game : ResetSource.Website);
            subject.Resets++;
        }

        var limited = engine.PlanReset(subject, beforeMidnight, ResetSource.Game);
        Assert.That((limited.Result, limited.Today, limited.DailyLimit), Is.EqualTo((ResetResult.DailyLimit, 2, 2)));
        Assert.That(engine.PlanReset(subject, SydneyMidnight, ResetSource.Game).Result, Is.EqualTo(ResetResult.Ok));
        Assert.That(engine.RecordOf("c1", SydneyMidnight).DayResets, Is.EqualTo(0));
    }

    /// <summary>
    /// A VIP resets at a lower level, pays less, may do more resets a day and gets more points; a VIP value below the
    /// minimum doesn't count.
    /// </summary>
    [Test]
    public void AVipGetsTheAdvantages()
    {
        var configuration = Configuration();
        configuration.Tiers[0].DailyLimit = 1;
        configuration.Tiers[0].Coins = 100;
        configuration.Vip = new ResetVipOptions { Enabled = true, MinimumVipLevel = 1, LevelReduction = 50, ZenDiscountPercent = 50, CoinsDiscountPercent = 25, ExtraDailyResets = 2, ExtraPointsPercent = 10 };
        var engine = Engine(configuration);

        var normal = engine.PlanReset(Subject(level: 260, resets: 0), SydneyMidnight, ResetSource.Game);
        Assert.That((normal.Result, normal.Vip, normal.DailyLimit), Is.EqualTo((ResetResult.LevelTooLow, false, 1)));

        var vip = engine.PlanReset(Subject(level: 260, resets: 0, vip: 1), SydneyMidnight, ResetSource.Game);
        Assert.That(vip.Result, Is.EqualTo(ResetResult.Ok));
        Assert.That((vip.Vip, vip.RequiredLevel, vip.Zen, vip.Coins.Single().Amount, vip.DailyLimit, vip.Points), Is.EqualTo((true, 250, 500L, 75, 3, 110)));

        configuration.Vip.MinimumVipLevel = 2;
        Assert.That(engine.PlanReset(Subject(level: 260, resets: 0, vip: 1), SydneyMidnight, ResetSource.Game).Vip, Is.False);
        configuration.Vip.Enabled = false;
        Assert.That(engine.PlanReset(Subject(level: 260, resets: 0, vip: 5), SydneyMidnight, ResetSource.Game).Vip, Is.False);
    }

    /// <summary>
    /// The relife needs its resets, level and costs; the resets go back to the kept share, the relife count goes up,
    /// the free points are the kept resets' points plus the bonus of every relife; the maximum and the daily limit count.
    /// </summary>
    [Test]
    public void TheRelifeKeepsAShareAndGivesThePermanentBonus()
    {
        var configuration = Configuration();
        configuration.Relife = new RelifeOptions { Enabled = true, ResetsNeeded = 15, RequiredLevel = 400, Zen = 1_000, KeepResetsPercent = 20, PointsPerRelife = 1_000, MaximumRelifes = 2, DailyLimit = 1 };
        var engine = Engine(configuration);

        Assert.That(engine.PlanRelife(Subject(level: 400, resets: 14), SydneyMidnight).Result, Is.EqualTo(ResetResult.ResetsTooLow));
        Assert.That(engine.PlanRelife(Subject(level: 399, resets: 15), SydneyMidnight).Result, Is.EqualTo(ResetResult.LevelTooLow));
        var subject = Subject(level: 400, resets: 15);
        var plan = engine.PlanRelife(subject, SydneyMidnight);
        Assert.That(plan.Result, Is.EqualTo(ResetResult.Ok));

        // 20 % of 15 resets = 3 kept (3 x 100 points) + 1 relife x 1000.
        Assert.That((plan.ResetsAfter, plan.RelifesAfter, plan.Points, plan.ClearStats, plan.Zen), Is.EqualTo((3, 1, 1_300, true, 1_000L)));
        engine.Record(subject, plan, SydneyMidnight, ResetSource.Game);
        Assert.That(engine.Relifes("c1"), Is.EqualTo(1));
        Assert.That(engine.PlanRelife(subject, SydneyMidnight.AddHours(1)).Result, Is.EqualTo(ResetResult.RelifeDailyLimit));

        engine.Store.Data.Characters["c1"].Relifes = 2;
        Assert.That(engine.PlanRelife(subject, SydneyMidnight.AddDays(1)).Result, Is.EqualTo(ResetResult.MaximumRelifes));
        configuration.Relife.Enabled = false;
        Assert.That(engine.PlanRelife(subject, SydneyMidnight.AddDays(1)).Result, Is.EqualTo(ResetResult.RelifeOff));

        // The relife bonus is part of every later reset's total.
        Assert.That(engine.PlanReset(Subject(level: 400, resets: 3), SydneyMidnight.AddDays(1), ResetSource.Game).Points, Is.EqualTo((4 * 100) + (2 * 1_000)));
        Assert.That(engine.RelifeRanking(10).Single().Value, Is.EqualTo(2));
    }

    /// <summary>
    /// A delegated reset pays the extra costs and has its own daily limit on top of the tier's.
    /// </summary>
    [Test]
    public void ADelegatedResetPaysTheExtraAndHasItsOwnLimit()
    {
        var configuration = Configuration();
        configuration.Delegated = new DelegatedResetOptions { Enabled = true, ExtraZen = 500, ExtraCoins = 5, ExtraCoinType = CashShopCoinType.WCoinP, DailyLimit = 1 };
        var engine = Engine(configuration);
        var subject = Subject(level: 400, resets: 0, zen: 10_000, coins: 0);
        var plan = engine.PlanReset(subject, SydneyMidnight, ResetSource.Delegated);
        Assert.That(plan.Zen, Is.EqualTo(1_500L));
        Assert.That(plan.Result, Is.EqualTo(ResetResult.NotEnoughCoins));

        subject.Coins = new Dictionary<CashShopCoinType, int> { [CashShopCoinType.WCoinP] = 5 };
        plan = engine.PlanReset(subject, SydneyMidnight, ResetSource.Delegated);
        Assert.That(plan.Result, Is.EqualTo(ResetResult.Ok));
        engine.Record(subject, plan, SydneyMidnight, ResetSource.Delegated);
        subject.Resets = 1;
        Assert.That(engine.PlanReset(subject, SydneyMidnight, ResetSource.Delegated).Result, Is.EqualTo(ResetResult.DelegatedDailyLimit));
        Assert.That(engine.PlanReset(subject, SydneyMidnight, ResetSource.Game).Result, Is.EqualTo(ResetResult.Ok));
        Assert.That(engine.TakeDelegatedUntold("c1"), Is.EqualTo(1));
        Assert.That(engine.TakeDelegatedUntold("c1"), Is.EqualTo(0));
        Assert.That(engine.Log(10, null, ResetSource.Delegated).Single().CoinsText, Is.EqualTo("5 WCoinP"));
    }

    /// <summary>
    /// The monthly ranking counts the resets of the Sydney month; the store keeps everything in its file.
    /// </summary>
    [Test]
    public void TheMonthlyRankingAndTheFile()
    {
        var file = Path.Combine(Path.GetTempPath(), $"hnmo-resets-{Guid.NewGuid():N}.json");
        try
        {
            var store = new ResetStore(file);
            var engine = new ResetEngine(Configuration(), store);
            var ann = Subject(level: 400, resets: 0);
            var bob = Subject(level: 400, resets: 0, id: "c2", name: "Bob");
            Do(engine, ann, SydneyMidnight.AddDays(-20)); // 27 October in Sydney.
            Do(engine, ann, SydneyMidnight);
            Do(engine, bob, SydneyMidnight);
            Do(engine, bob, SydneyMidnight.AddHours(1));
            lock (store.Lock)
            {
                store.SaveIfChanged();
            }

            var copy = new ResetEngine(Configuration(), new ResetStore(file));
            var november = copy.MonthlyRanking("2026-11", 10);
            Assert.That(november.Select(e => (e.Rank, e.Name, e.Value)), Is.EqualTo(new[] { (1, "Bob", 2), (2, "Ann", 1) }));
            Assert.That(copy.MonthlyRanking("2026-10", 10).Single().Value, Is.EqualTo(1));
            Assert.That(copy.Log(10).Count, Is.EqualTo(4));
        }
        finally
        {
            File.Delete(file);
        }
    }

    /// <summary>
    /// A reset in the game pays the zen, coins and items, sets the reset count, the level and the points, clears the
    /// stats and is remembered; a failing check takes nothing.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task AResetInTheGamePaysAndChangesTheCharacterAsync()
    {
        var player = await PlayerAsync().ConfigureAwait(false);
        var configuration = Configuration();
        configuration.Tiers[0].Coins = 7;
        configuration.Tiers[0].Items.Add(new ResetItemCost { Group = 14, Number = 13, Count = 1 });
        var plugIn = new ResetOptionsPlugIn { Configuration = configuration };
        player.Attributes![Stats.Level] = 300;
        player.Attributes[Stats.BaseStrength] = 500;
        player.Money = 3_000;
        player.Account!.GoblinPoints = 10;

        Assert.That(await plugIn.ResetAsync(player).ConfigureAwait(false), Is.False);
        Assert.That((player.Money, player.Account.GoblinPoints), Is.EqualTo((3_000, 10)));

        await AddItemAsync(player, 14, 13).ConfigureAwait(false);
        Assert.That(await plugIn.ResetAsync(player).ConfigureAwait(false), Is.True);
        Assert.That((int)player.Attributes[Stats.Resets], Is.EqualTo(1));
        Assert.That((int)player.Attributes[Stats.Level], Is.EqualTo(1));
        Assert.That((int)player.Attributes[Stats.BaseStrength], Is.EqualTo(28));
        Assert.That(player.SelectedCharacter!.LevelUpPoints, Is.EqualTo(100));
        Assert.That((player.Money, player.Account.GoblinPoints), Is.EqualTo((2_000, 3)));
        Assert.That(player.Inventory!.Items.Count(i => i.Definition?.Number == 13), Is.EqualTo(0));
        Assert.That(plugIn.GetEngine().RecordOf(player.SelectedCharacter.Id.ToString(), DateTime.UtcNow).DayResets, Is.EqualTo(1));
    }

    /// <summary>
    /// While a character is offline leveling with the delegated reset on, the server resets it when the level is
    /// reached, pays the costs, keeps the build (the points go back in the same shares) and remembers it for the player.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ADelegatedResetDuringOfflineLevelingKeepsTheBuildAsync()
    {
        var context = GameContextTestHelper.CreateGameContext();
        var player = await PlayerTestHelper.CreateOfflineLevelingPlayerAsync(context).ConfigureAwait(false);
        player.Account!.LoginName = "ann";
        player.SelectedCharacter!.Id = Guid.NewGuid();
        player.SelectedCharacter.Name = "AnnKnight";
        var configuration = Configuration();
        configuration.Tiers[0].Points = 1_000;
        configuration.Delegated = new DelegatedResetOptions { Enabled = true, ExtraZen = 100, KeepBuild = true };
        var plugIn = new ResetOptionsPlugIn { Configuration = configuration };
        var engine = plugIn.GetEngine();
        var subject = ResetOptionsPlugIn.SubjectOf(player)!;

        player.Attributes![Stats.Level] = 300;
        player.Money = 5_000;
        Assert.That(await plugIn.TryDelegatedResetAsync(player).ConfigureAwait(false), Is.False, "the delegated reset is off");

        engine.SetDelegated(subject, true);
        player.Attributes[Stats.Level] = 299;
        Assert.That(await plugIn.TryDelegatedResetAsync(player).ConfigureAwait(false), Is.False, "the level isn't reached");

        // Strength 28 + 300, agility 20 + 100: the 1000 points go 3:1.
        player.Attributes[Stats.Level] = 300;
        player.Attributes[Stats.BaseStrength] = 328;
        player.Attributes[Stats.BaseAgility] = 120;
        Assert.That(await plugIn.TryDelegatedResetAsync(player).ConfigureAwait(false), Is.True);
        Assert.That((int)player.Attributes[Stats.Resets], Is.EqualTo(1));
        Assert.That((int)player.Attributes[Stats.Level], Is.EqualTo(1));
        Assert.That(player.Money, Is.EqualTo(3_900));
        Assert.That((int)player.Attributes[Stats.BaseStrength], Is.EqualTo(28 + 750));
        Assert.That((int)player.Attributes[Stats.BaseAgility], Is.EqualTo(20 + 250));
        Assert.That(player.SelectedCharacter.LevelUpPoints, Is.EqualTo(0));
        Assert.That(engine.Log(5, null, ResetSource.Delegated).Single().Name, Is.EqualTo("AnnKnight"));
        Assert.That(engine.RecordOf(subject.CharacterId, DateTime.UtcNow).DelegatedUntold, Is.EqualTo(1));

        player.Money = 0;
        player.Attributes[Stats.Level] = 300;
        Assert.That(await plugIn.TryDelegatedResetAsync(player).ConfigureAwait(false), Is.False);
        Assert.That(engine.RecordOf(subject.CharacterId, DateTime.UtcNow).DelegatedBlocked, Is.EqualTo(nameof(ResetResult.NotEnoughZen)));
    }

    /// <summary>
    /// Clearing the stats gives the points back as free points.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ClearingTheStatsGivesThePointsBackAsync()
    {
        var player = await PlayerAsync().ConfigureAwait(false);
        var attributes = player.Attributes!;
        attributes[Stats.BaseStrength] = 100;
        attributes[Stats.BaseEnergy] = 30;
        player.SelectedCharacter!.LevelUpPoints = 5;
        var back = ResetApplier.ClearStats(player.SelectedCharacter, a => attributes[a], (a, v) => attributes[a] = v);
        Assert.That(back, Is.EqualTo(72 + 20));
        Assert.That(player.SelectedCharacter.LevelUpPoints, Is.EqualTo(97));
        Assert.That((int)attributes[Stats.BaseStrength], Is.EqualTo(28));
    }

    private static void Do(ResetEngine engine, ResetSubject subject, DateTime nowUtc)
    {
        var plan = engine.PlanReset(subject, nowUtc, ResetSource.Game);
        Assert.That(plan.Result, Is.EqualTo(ResetResult.Ok));
        engine.Record(subject, plan, nowUtc, ResetSource.Game);
        subject.Resets++;
    }

    private static async Task<Player> PlayerAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Account!.LoginName = "ann";
        player.SelectedCharacter!.Id = Guid.NewGuid();
        player.SelectedCharacter.Name = "AnnKnight";
        return player;
    }

    private static async Task AddItemAsync(Player player, short group, short number)
    {
        var item = player.PersistenceContext.CreateNew<Item>();
        item.Definition = new ItemDefinition { Name = "Jewel", Group = (byte)group, Number = number, Width = 1, Height = 1 };
        item.Durability = 1;
        Assert.That(await player.Inventory!.AddItemAsync(item).ConfigureAwait(false), Is.True);
    }

    private static ResetOptionsConfiguration Configuration() => new()
    {
        FileName = string.Empty,
        MoveHome = false,
        LogOut = false,
        Vip = new ResetVipOptions { Enabled = false },
        Tiers =
        [
            new ResetTier { FromResets = 0, ToResets = 9, RequiredLevel = 300, LevelAfterReset = 1, Zen = 1_000, Points = 100, PointsMode = ResetPointsMode.Total, ClearStats = true },
            new ResetTier { FromResets = 10, ToResets = 19, RequiredLevel = 350, LevelAfterReset = 1, Zen = 2_000, Points = 200, PointsMode = ResetPointsMode.Total, ClearStats = true },
        ],
        Relife = new RelifeOptions { Enabled = true, ResetsNeeded = 15, PointsPerRelife = 1_000 },
        Delegated = new DelegatedResetOptions { Enabled = true },
    };

    private static ResetEngine Engine(ResetOptionsConfiguration configuration) => new(configuration, new ResetStore());

    private static ResetSubject Subject(int level, int resets, long zen = 1_000_000, int coins = 1_000, int items = 0, int vip = 0, string id = "c1", string name = "Ann") => new()
    {
        CharacterId = id,
        Name = name,
        Login = id == "c1" ? "ann" : "bob",
        Level = level,
        Resets = resets,
        Zen = zen,
        Coins = new Dictionary<CashShopCoinType, int> { [CashShopCoinType.GoblinPoints] = coins },
        VipLevel = vip,
        CountItems = _ => items,
    };
}
