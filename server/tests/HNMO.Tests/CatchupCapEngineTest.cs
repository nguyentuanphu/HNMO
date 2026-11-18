// <copyright file="CatchupCapEngineTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using HNMO.GameLogic.PlugIns.CatchupCap;
using HNMO.GameLogic.PlugIns.ResetOptions;

/// <summary>
/// Unit tests for <see cref="CatchupCapEngine"/> and its integration with <see cref="ResetEngine"/>.
/// </summary>
[TestFixture]
public class CatchupCapEngineTest
{
    private string _tempDir = string.Empty;

    /// <summary>Creates an isolated temp directory.</summary>
    [SetUp]
    public void SetUp()
    {
        this._tempDir = Path.Combine(Path.GetTempPath(), $"hnmo-cu-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this._tempDir);
    }

    /// <summary>Cleans up temp directory.</summary>
    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(this._tempDir))
        {
            Directory.Delete(this._tempDir, recursive: true);
        }
    }

    /// <summary>Level cap holds at InitialLevelCap until the first player reaches it, then grows by N per Sydney day.</summary>
    [Test]
    public void LevelCapRisesPerSydneyDayAfterFirstPlayerReachesCap()
    {
        var cfg = new CatchupCapConfiguration
        {
            FileName = Path.Combine(this._tempDir, "hnmo-state.json"),
            InitialLevelCap = 300,
            LevelsPerDay = 15,
            HardMaxLevelCap = 400,
        };
        var engine = new CatchupCapEngine(cfg, CatchupCapStore.Open(cfg.FileName), enabled: true);
        var day0 = new DateTime(2026, 11, 16, 2, 0, 0, DateTimeKind.Utc); // 13:00 Sydney on Nov 16

        // Below trigger level: stays 300
        Assert.That(engine.ObserveLevel("Knight", 299, day0), Is.False);
        Assert.That(engine.GetSnapshot(day0.AddDays(5)).TodayLevelCap, Is.EqualTo(300));

        // First player hits 300 -> triggered on Nov 16 Sydney
        Assert.That(engine.ObserveLevel("HeroOne", 300, day0), Is.True);
        Assert.That(engine.ObserveLevel("HeroTwo", 300, day0.AddHours(1)), Is.False);

        var snap0 = engine.GetSnapshot(day0);
        Assert.That(snap0.TodayLevelCap, Is.EqualTo(300));
        Assert.That(snap0.LevelFirstPlayer, Is.EqualTo("HeroOne"));

        // +1 Sydney day -> 315, +2 days -> 330, +10 days -> clamped at HardMax 400
        Assert.That(engine.GetSnapshot(day0.AddDays(1)).TodayLevelCap, Is.EqualTo(315));
        Assert.That(engine.GetSnapshot(day0.AddDays(2)).TodayLevelCap, Is.EqualTo(330));
        Assert.That(engine.GetSnapshot(day0.AddDays(10)).TodayLevelCap, Is.EqualTo(400));
    }

    /// <summary>Reset cap mode enforces daily reset limit dynamically through ResetEngine.</summary>
    [Test]
    public void DailyResetCapRestrictsResetEngineAndGrowsDaily()
    {
        var cuCfg = new CatchupCapConfiguration
        {
            FileName = Path.Combine(this._tempDir, "hnmo-state.json"),
            ResetCapEnabled = true,
            InitialDailyResets = 2,
            DailyResetsPerDay = 1,
            HardMaxDailyResets = 10,
            TriggerResets = 1,
        };
        var cu = new CatchupCapEngine(cuCfg, CatchupCapStore.Open(cuCfg.FileName), enabled: true);
        var day0 = new DateTime(2026, 11, 16, 3, 0, 0, DateTimeKind.Utc);
        cu.ObserveResets("ResetKing", 1, day0);
        Assert.That(cu.GetSnapshot(day0).TodayDailyResetCap, Is.EqualTo(2));
        Assert.That(cu.GetSnapshot(day0.AddDays(3)).TodayDailyResetCap, Is.EqualTo(5));

        var rstCfg = new ResetOptionsConfiguration
        {
            FileName = Path.Combine(this._tempDir, "resets.json"),
            Tiers = [new ResetTier { FromResets = 0, RequiredLevel = 1, DailyLimit = 99 }],
        };
        var resetEngine = new ResetEngine(rstCfg, ResetStore.Open(rstCfg.FileName));
        var subject = new ResetSubject { CharacterId = "c1", Name = "ResetKing", Login = "u1", Level = 400, Resets = 0 };

        // Do 2 resets on day 0
        for (var i = 0; i < 2; i++)
        {
            var p = resetEngine.PlanReset(subject, day0, ResetSource.Game, cu.GetSnapshot(day0).TodayDailyResetCap);
            Assert.That(p.Result, Is.EqualTo(ResetResult.Ok));
            resetEngine.Record(subject, p, day0, ResetSource.Game);
            subject.Resets++;
        }

        // 3rd reset on day 0 hits TodayDailyResetCap=2
        var blocked = resetEngine.PlanReset(subject, day0, ResetSource.Game, cu.GetSnapshot(day0).TodayDailyResetCap);
        Assert.That(blocked.Result, Is.EqualTo(ResetResult.DailyLimit));

        // On day 1 (TodayDailyResetCap=3) it resets again cleanly
        var day1 = day0.AddDays(1);
        var nextDay = resetEngine.PlanReset(subject, day1, ResetSource.Game, cu.GetSnapshot(day1).TodayDailyResetCap);
        Assert.That(nextDay.Result, Is.EqualTo(ResetResult.Ok));
    }
}
