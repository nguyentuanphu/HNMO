// <copyright file="GameplayRulesTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Globalization;
using System.Text.Json;
using Moq;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.GameplayRules;
using HNMO.GameLogic.Views;
using HNMO.Interfaces;
using HNMO.Pathfinding;
using HNMO.PlugIns;

/// <summary>
/// Unit and game-context tests for Per-Map Rules (14.1), Bonus Events (14.2), and Combat Limits (14.3).
/// </summary>
[TestFixture]
public sealed class GameplayRulesTests
{
    /// <summary>Verifies per-map PvP block, PK penalty exemption, EXP bonus, and zen drop multiplier.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async ValueTask MapRulesEnforcePvpPkExpAndDropsAsync()
    {
        var cfg = new PlugInConfiguration
        {
            TypeId = typeof(MapRulesPlugIn).GUID,
            IsActive = true,
            CustomConfiguration = JsonSerializer.Serialize(new MapRulesConfiguration
            {
                Enabled = true,
                Rules =
                [
                    new MapRuleEntry
                    {
                        MapNumber = 0,
                        MapName = "Lorencia",
                        PvpEnabled = false,
                        PkPenaltyEnabled = false,
                        ExpBonusPercent = 100,
                        ItemDropBonusPercent = 100,
                        ZenDropBonusPercent = 50,
                    },
                ],
            }),
        };

        var context = GameContextTestHelper.CreateGameContext([cfg]);
        var p1 = await CreatePlayerAsync(context, "en").ConfigureAwait(false);
        var p2 = await CreatePlayerAsync(context, "vi").ConfigureAwait(false);
        p1.Position = new Point(10, 10);
        p2.Position = new Point(11, 10);

        // PvP is disabled by map rule even outside safezone
        var hit = await p2.AttackByAsync(p1, null, false).ConfigureAwait(false);
        Assert.That(hit, Is.Null);

        // PK penalty exemption checks true on Map 0
        var plugIn = MapRulesPlugIn.Get(context)!;
        var pkArgs = new PlayerKillPenaltyExemptionArgs();
        plugIn.CheckExemption(p1, p2, pkArgs);
        Assert.That(pkArgs.IsExempted, Is.True);

        // EXP scales by +100% (x2)
        var expArgs = new ExperienceCalculationArgs(p2, false, 1000);
        await plugIn.CalculateExperienceAsync(p1, expArgs).ConfigureAwait(false);
        Assert.That(expArgs.Experience, Is.EqualTo(2000).Within(0.01));

        // Drop generator scales money by +50% and rolls extra drops for +100%
        var monster = new HNMO.Persistence.BasicModel.MonsterDefinition { NumberOfMaximumItemDrops = 1 };
        monster.Attributes.Add(new HNMO.Persistence.BasicModel.MonsterAttribute { AttributeDefinition = Stats.Level, Value = 10 });
        var moneyGroup = new HNMO.Persistence.BasicModel.DropItemGroup { Chance = 1.0, ItemType = SpecialItemType.Money };
        monster.DropItemGroups.Add(moneyGroup);
        var gen = new DefaultDropGenerator(context.Configuration, Rand.GetRandomizer());
        var (_, money) = await gen.GenerateItemDropsAsync(monster, 100, p1).ConfigureAwait(false);
        // Money = (100+7)=107 * 1.5 = 160 or 161: the +50 % zen rule applies once; the extra roll of the +100 % item
        // drop rule gives items only (it added its zen too, so an item bonus raised the zen).
        Assert.That(money, Is.InRange(160u, 161u));
    }

    /// <summary>Verifies Sydney time happy hour evaluation (including midnight wraparound), chat announcements, and timetable entries.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async ValueTask BonusEventsEvaluateSydneyTimeAnnounceAndExposeTimetableAsync()
    {
        // 2026-11-16 12:30 UTC == 2026-11-16 23:30 AEDT (Sydney)
        var utcStart = new DateTimeOffset(2026, 11, 16, 12, 30, 0, TimeSpan.Zero);
        var ev = new BonusEventEntry
        {
            Id = "late",
            NameEn = "Midnight Bonus",
            NameVi = "Giờ Vàng Đêm",
            MapNumber = 0,
            StartTimeSydney = "23:15",
            DurationMinutes = 90,
            ExpBonusPercent = 50,
            ItemDropBonusPercent = 25,
            ZenDropBonusPercent = 40,
            Enabled = true,
        };

        Assert.That(BonusEventsPlugIn.IsWindowActive(ev, utcStart.UtcDateTime, 0), Is.True);
        Assert.That(BonusEventsPlugIn.IsWindowActive(ev, utcStart.UtcDateTime.AddHours(1), 0), Is.True, "Cross-midnight wrap in Sydney");
        Assert.That(BonusEventsPlugIn.IsWindowActive(ev, utcStart.UtcDateTime.AddHours(2), 0), Is.False);
        Assert.That(BonusEventsPlugIn.IsWindowActive(ev, utcStart.UtcDateTime, 2), Is.False);

        var pluginCfg = new PlugInConfiguration
        {
            TypeId = typeof(BonusEventsPlugIn).GUID,
            IsActive = true,
            CustomConfiguration = JsonSerializer.Serialize(new BonusEventsConfiguration
            {
                Enabled = true,
                AnnounceInChat = true,
                Events = [ev],
            }),
        };

        var clock = new FixedTimeProvider(utcStart);
        var plugIn = new BonusEventsPlugIn
        {
            TimeProviderOverride = clock,
            Configuration = new BonusEventsConfiguration { Enabled = true, AnnounceInChat = true, Events = [ev] },
        };

        var context = (GameContext)GameContextTestHelper.CreateGameContext([pluginCfg]);
        var pVi = await CreatePlayerAsync(context, "vi").ConfigureAwait(false);
        await context.AddPlayerAsync(pVi).ConfigureAwait(false);
        await plugIn.ExecuteTaskAsync(context).ConfigureAwait(false);

        Mock.Get(pVi.ViewPlugIns.GetPlugIn<IShowMessagePlugIn>()!)
            .Verify(v => v.ShowMessageAsync(It.Is<string>(s => s.Contains("Giờ Vàng Đêm", StringComparison.Ordinal)), MessageType.GoldenCenter), Times.Once);

        var timetable = plugIn.GetTimetableEntries(utcStart.UtcDateTime);
        Assert.That(timetable.Single().IsActive, Is.True);
        Assert.That(timetable.Single().ExpBonusPercent, Is.EqualTo(50));
    }

    /// <summary>
    /// Verifies that a bonus event and a running happy hour don't stack: only the larger EXP bonus applies.
    /// The happy hour is already part of the experience the plug-in receives (through the EXP rate attribute).
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async ValueTask BonusEventsDoNotStackWithHappyHourAsync()
    {
        var utcNow = new DateTimeOffset(2026, 11, 16, 12, 30, 0, TimeSpan.Zero); // 23:30 in Sydney
        var happyHourConfig = new PlugInConfiguration { TypeId = typeof(HNMO.GameLogic.PlugIns.PeriodicTasks.HappyHourPlugIn).GUID, IsActive = true };
        var context = GameContextTestHelper.CreateGameContext([happyHourConfig]);
        var player = await CreatePlayerAsync(context, "en").ConfigureAwait(false);
        var happyHour = context.PlugInManager.GetActivePlugInsOf<IPeriodicTaskPlugIn>().OfType<HNMO.GameLogic.PlugIns.PeriodicTasks.HappyHourPlugIn>().Single();

        BonusEventsPlugIn CreateBonusEvents(int expBonusPercent) => new()
        {
            TimeProviderOverride = new FixedTimeProvider(utcNow),
            Configuration = new BonusEventsConfiguration
            {
                Enabled = true,
                Events = [new BonusEventEntry { Id = "b", MapNumber = 0, StartTimeSydney = "23:00", DurationMinutes = 60, ExpBonusPercent = expBonusPercent, Enabled = true }],
            },
        };

        async ValueTask<double> CalculateAsync(BonusEventsPlugIn plugIn, double experience)
        {
            var args = new ExperienceCalculationArgs(player, false, experience);
            await plugIn.CalculateExperienceAsync(player, args).ConfigureAwait(false);
            return args.Experience;
        }

        // Without a happy hour, the bonus event applies fully: 1000 * 2.
        Assert.That(await CalculateAsync(CreateBonusEvents(100), 1000).ConfigureAwait(false), Is.EqualTo(2000).Within(0.01));

        // With a 1.5x happy hour the received experience is already 1500.
        happyHour.GetMultiplierElement(context).Value = 1.5f;

        // The larger bonus event (2x) wins: 1000 * 2, not 1000 * 1.5 * 2.
        Assert.That(await CalculateAsync(CreateBonusEvents(100), 1500).ConfigureAwait(false), Is.EqualTo(2000).Within(0.01));

        // The larger happy hour wins over a smaller bonus event (1.2x): 1000 * 1.5.
        Assert.That(await CalculateAsync(CreateBonusEvents(20), 1500).ConfigureAwait(false), Is.EqualTo(1500).Within(0.01));
    }

    /// <summary>Verifies damage cap per hit, PvP reduction %, and Valley of Loren Castle Siege reduction %.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async ValueTask CombatLimitsCapAndReducePvpAndSiegeDamageAsync()
    {
        var cfg = new PlugInConfiguration
        {
            TypeId = typeof(CombatLimitsPlugIn).GUID,
            IsActive = true,
            CustomConfiguration = JsonSerializer.Serialize(new CombatLimitsConfiguration
            {
                Enabled = true,
                MaxDamagePerHit = 300,
                PvpDamageReductionPercent = 50,
                CastleSiegeDamageReductionPercent = 20,
            }),
        };

        var context = GameContextTestHelper.CreateGameContext([cfg]);
        var lorencia = await context.GetMapAsync(0).ConfigureAwait(false);
        var p1 = await CreatePlayerAsync(context, "en").ConfigureAwait(false);
        var p2 = await CreatePlayerAsync(context, "en").ConfigureAwait(false);

        // PvP: 1000 -> 50% off -> 500 -> capped at 300
        Assert.That(CombatLimitsPlugIn.ApplyDamageLimits(p1, p2, isPvp: true, 1000), Is.EqualTo(300));
        // PvP: 400 -> 50% off -> 200 (< 300 cap)
        Assert.That(CombatLimitsPlugIn.ApplyDamageLimits(p1, p2, isPvp: true, 400), Is.EqualTo(200));

        // Castle Siege map (30): 1000 * 0.5 * 0.8 = 400 -> capped 300; or 400 * 0.5 * 0.8 = 160
        var lorenDef = new HNMO.Persistence.BasicModel.GameMapDefinition { Number = 30, Name = "Valley of Loren", IsActive = true, TerrainData = lorencia!.Definition.TerrainData };
        context.Configuration.Maps.Add(lorenDef);
        await p2.WarpToAsync(new ExitGate { Map = lorenDef, X1 = 10, X2 = 10, Y1 = 10, Y2 = 10 }).ConfigureAwait(false);
        await p2.ClientReadyAfterMapChangeAsync().ConfigureAwait(false);
        Assert.That(CombatLimitsPlugIn.ApplyDamageLimits(p1, p2, isPvp: true, 400), Is.EqualTo(160));
    }

    private static async ValueTask<Player> CreatePlayerAsync(IGameContext context, string lang)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        player.Account = new HNMO.Persistence.BasicModel.Account
        {
            LoginName = "u" + Guid.NewGuid().ToString("N")[..4],
            LanguageIsoCode = lang,
        };
        player.SelectedCharacter!.Name = "C" + Guid.NewGuid().ToString("N")[..4];
        return player;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
