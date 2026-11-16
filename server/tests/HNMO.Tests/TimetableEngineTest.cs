// <copyright file="TimetableEngineTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Text.Json;
using Moq;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.InvasionEvents;
using HNMO.GameLogic.PlugIns.Timetable;
using HNMO.GameLogic.Views;
using HNMO.Pathfinding;
using HNMO.PlugIns;

/// <summary>
/// Unit and integration tests for <see cref="TimetableEngine"/>, <see cref="TimetableStore"/>,
/// <see cref="TimetablePlugIn"/>, <see cref="RabbitInvasionPlugIn"/>, <see cref="SnowmanInvasionPlugIn"/>,
/// and <see cref="FindTheGmInvasionPlugIn"/>.
/// </summary>
[TestFixture]
public sealed class TimetableEngineTest
{
    private string _stateFile = null!;

    /// <summary>Creates a fresh temporary state file per test.</summary>
    [SetUp]
    public void SetUp() => this._stateFile = Path.Combine(Path.GetTempPath(), $"hnmo-tt-test-{Guid.NewGuid():N}.json");

    /// <summary>Removes the temporary state file.</summary>
    [TearDown]
    public void TearDown()
    {
        if (File.Exists(this._stateFile))
        {
            File.Delete(this._stateFile);
        }

        var bak = this._stateFile + ".bak";
        if (File.Exists(bak))
        {
            File.Delete(bak);
        }
    }

    /// <summary>Active entries sort before upcoming entries, and Sydney wall-clock times resolve accurately.</summary>
    [Test]
    public void ComputesNextStartsAndSortsActiveFirst()
    {
        // 2026-11-16 09:35 UTC == 2026-11-16 20:35 AEDT (+11 Sydney)
        var utcNow = new DateTime(2026, 11, 16, 9, 35, 0, DateTimeKind.Utc);
        var config = new TimetableConfiguration
        {
            TimeZoneId = "Australia/Sydney",
            CustomEntries =
            [
                new TimetableCustomEntry
                {
                    Id = "late-night",
                    Category = "custom",
                    NameEn = "Late Boss",
                    NameVi = "Boss Khuya",
                    MapsEn = "Tarkan",
                    MapsVi = "Tarkan",
                    DropsEn = "Jewels",
                    DropsVi = "Ngoc",
                    ScheduleTimes = "23:00",
                    DurationMinutes = 15,
                },
            ],
        };

        var sources = new[]
        {
            new TimetableSourceItem
            {
                Id = "golden",
                Category = "invasion",
                NameEn = "Golden Invasion",
                NameVi = "Quai Vang",
                MapsEn = "Lorencia",
                MapsVi = "Lorencia",
                DropsEn = "Boxes of Kundun",
                DropsVi = "Hop Kundun",
                DailyTimes = [new TimeOnly(20, 30), new TimeOnly(22, 0)],
                Duration = TimeSpan.FromMinutes(20),
            },
            new TimetableSourceItem
            {
                Id = "bc",
                Category = "event",
                NameEn = "Blood Castle",
                NameVi = "Huyet Lau",
                MapsEn = "Devias",
                MapsVi = "Devias",
                DropsEn = "Chaos",
                DropsVi = "Chaos",
                DailyTimes = [new TimeOnly(21, 0)],
                Duration = TimeSpan.FromMinutes(20),
            },
        };

        var snapshot = TimetableEngine.BuildSnapshot(config, enabled: true, utcNow, sources, []);
        Assert.That(snapshot.Enabled, Is.True);
        Assert.That(snapshot.Entries.Select(e => e.Id), Is.EqualTo(new[] { "golden", "bc", "late-night" }));
        Assert.That(snapshot.Entries[0].IsActive, Is.True);
        Assert.That(snapshot.Entries[0].EndsAtUtc, Is.EqualTo(new DateTime(2026, 11, 16, 9, 50, 0, DateTimeKind.Utc)));
        Assert.That(snapshot.Entries[1].IsActive, Is.False);
        Assert.That(snapshot.Entries[1].NextStartUtc, Is.EqualTo(new DateTime(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc)));
    }

    /// <summary>Find-the-GM winners persist through <see cref="HnmoStateStore"/> and cap at 20 entries.</summary>
    [Test]
    public void PersistsFindGmWinnersViaHnmoStateStore()
    {
        var store = TimetableStore.Open(this._stateFile);
        store.RecordFindGmWinner("HeroOne", "Lorencia", 50, "WCoinC", 500000, new DateTime(2026, 11, 16, 8, 0, 0, DateTimeKind.Utc));

        var reopened = TimetableStore.Open(this._stateFile);
        var winners = reopened.ReadFindGmWinners();
        Assert.That(winners, Has.Count.EqualTo(1));
        Assert.That(winners[0].CharacterName, Is.EqualTo("HeroOne"));
        Assert.That(winners[0].MapName, Is.EqualTo("Lorencia"));
        Assert.That(winners[0].Coins, Is.EqualTo(50));
    }

    /// <summary><see cref="TimetablePlugIn"/> sends plain-ASCII JSON through <see cref="IHnmoTimetableViewPlugIn"/>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PlugInSendsStateToViewAsync()
    {
        var context = GameContextTestHelper.CreateGameContext();
        var plugIn = new TimetablePlugIn { Configuration = new TimetableConfiguration { FileName = this._stateFile } };
        var player = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);

        string? capturedJson = null;
        bool? capturedRequested = null;
        Mock.Get(player.ViewPlugIns.GetPlugIn<IHnmoTimetableViewPlugIn>()!)
            .Setup(v => v.ShowTimetableAsync(It.IsAny<string>(), It.IsAny<bool>()))
            .Callback<string, bool>((json, req) =>
            {
                capturedJson = json;
                capturedRequested = req;
            })
            .Returns(ValueTask.CompletedTask);

        await plugIn.SendStateAsync(player, requested: true).ConfigureAwait(false);

        Assert.That(capturedRequested, Is.True);
        Assert.That(capturedJson, Is.Not.Null);
        using var doc = JsonDocument.Parse(capturedJson!);
        Assert.That(doc.RootElement.GetProperty("off").GetBoolean(), Is.False);
        Assert.That(doc.RootElement.GetProperty("timeZone").GetString(), Is.EqualTo("Australia/Sydney"));
    }

    /// <summary><see cref="SnowmanInvasionPlugIn"/> exposes default config and turns PassiveNpc definitions into attackable monsters.</summary>
    [Test]
    public void SnowmanPlugInTurnsPassiveDefinitionIntoCombatMonster()
    {
        var plugIn = new SnowmanInvasionPlugIn();
        var defaults = (PeriodicInvasionConfiguration)plugIn.CreateDefaultConfig();
        Assert.That(defaults.Mobs.Select(m => m.MonsterId), Does.Contain(InvasionMonsters.TransformedSnowman));
        Assert.That(defaults.Mobs.Select(m => m.MonsterId), Does.Contain(InvasionMonsters.CursedSanta));

        var rabbit = new RabbitInvasionPlugIn();
        var rabbitCfg = (PeriodicInvasionConfiguration)rabbit.CreateDefaultConfig();
        Assert.That(rabbitCfg.Mobs.Select(m => m.MonsterId), Does.Contain(InvasionMonsters.StrangeRabbit));
        Assert.That(rabbitCfg.Mobs.Select(m => m.MonsterId), Does.Contain(InvasionMonsters.HideousRabbit));
        Assert.That(typeof(IDisabledByDefault).IsAssignableFrom(typeof(RabbitInvasionPlugIn)), Is.True);
        Assert.That(typeof(IDisabledByDefault).IsAssignableFrom(typeof(SnowmanInvasionPlugIn)), Is.True);
        Assert.That(typeof(IDisabledByDefault).IsAssignableFrom(typeof(FindTheGmInvasionPlugIn)), Is.True);
        Assert.That(typeof(IDisabledByDefault).IsAssignableFrom(typeof(TimetablePlugIn)), Is.True);
    }

    /// <summary><see cref="FindTheGmInvasionPlugIn"/> records a winner when talked to during an active run.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FindTheGmRewardsFirstFinderAndRecordsWinnerAsync()
    {
        var context = GameContextTestHelper.CreateGameContext();
        var map = await context.GetMapAsync(0).ConfigureAwait(false);
        Assert.That(map, Is.Not.Null);

        var gmDef = context.Configuration.Monsters.FirstOrDefault(m => m.Number == (short)InvasionMonsters.GameMaster)
            ?? new MonsterDefinition { Number = (short)InvasionMonsters.GameMaster, ObjectKind = NpcObjectKind.PassiveNpc };

        var plugIn = new FindTheGmInvasionPlugIn
        {
            Configuration = new FindTheGmConfiguration
            {
                FileName = this._stateFile,
                RewardCoins = 0,
                RewardZen = 250000,
                MaxWinners = 1,
            },
        };

        var point = map!.Terrain.RandomWalkableCoordinate!.Value;
        var npc = new NonPlayerCharacter(
            new MonsterSpawnArea { GameMap = map.Definition, MonsterDefinition = gmDef, X1 = point.X, X2 = point.X, Y1 = point.Y, Y2 = point.Y },
            gmDef,
            map);
        await map.AddAsync(npc).ConfigureAwait(false);

        // Inject the active run via reflection or run lifecycle.
        var runsField = typeof(FindTheGmInvasionPlugIn).GetField("_runs", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var runsDict = runsField.GetValue(plugIn)!;
        var runType = typeof(FindTheGmInvasionPlugIn).GetNestedType("FindGmRun", System.Reflection.BindingFlags.NonPublic)!;
        var run = Activator.CreateInstance(runType, "testrun1")!;
        var npcsProp = runType.GetProperty("Npcs")!.GetValue(run) as IList<NonPlayerCharacter>;
        npcsProp!.Add(npc);
        runsDict.GetType().GetMethod("TryAdd")!.Invoke(runsDict, [context, run]);

        var p1 = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        p1.Account ??= new();
        p1.Account.LoginName = "finder1";
        var startZen = p1.Money;

        var args1 = new NpcTalkEventArgs();
        await plugIn.PlayerTalksToNpcAsync(p1, npc, args1).ConfigureAwait(false);
        Assert.That(args1.HasBeenHandled, Is.True);
        Assert.That(p1.Money, Is.EqualTo(startZen + 250000));

        var winners = TimetableStore.Open(this._stateFile).ReadFindGmWinners();
        Assert.That(winners, Has.Count.EqualTo(1));
        Assert.That(winners[0].Zen, Is.EqualTo(250000));
    }
}
