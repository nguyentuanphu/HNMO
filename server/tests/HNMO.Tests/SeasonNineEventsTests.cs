// <copyright file="SeasonNineEventsTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System;
using System.IO;
using System.Linq;
using HNMO.GameLogic.PlugIns.SeasonNineEvents;

/// <summary>
/// Unit tests for <see cref="SeasonNineEventsEngine"/>, <see cref="SeasonNineEventsStore"/>, and default suite config.
/// </summary>
[TestFixture]
public class SeasonNineEventsTests
{
    /// <summary>Verifies default Season 9 configuration disables plug-in by default, gates to S9, and populates 9 invasion tracks and 23 CCS slots.</summary>
    [Test]
    public void DefaultsMatchSeasonNineSuiteSchedules()
    {
        var cfg = new SeasonNineEventsConfiguration();
        Assert.Multiple(() =>
        {
            Assert.That(cfg.Enabled, Is.False);
            Assert.That(cfg.SeasonNineOnly, Is.True);
            Assert.That(cfg.ChaosCastleSurvivalDailyTimes, Has.Count.GreaterThanOrEqualTo(23));
            Assert.That(cfg.TormentedSquareDailyTimes, Has.Count.EqualTo(5));
            Assert.That(cfg.Invasions, Has.Count.EqualTo(9));
            Assert.That(cfg.Invasions.Any(i => i.MonsterNumber == 674), Is.True);
            Assert.That(cfg.Invasions.Any(i => i.MonsterNumber == 675), Is.True);
            Assert.That(cfg.Invasions.Any(i => i.MonsterNumber == 676), Is.True);
            Assert.That(cfg.Invasions.Any(i => i.MonsterNumber == 691), Is.True);
            Assert.That(SeasonNineEventsEngine.AcheronObelisks, Has.Count.EqualTo(3));
            Assert.That(SeasonNineEventsEngine.TormentedSquareRounds, Has.Count.EqualTo(15));
            Assert.That(SeasonNineEventsEngine.UrukMountainGuardMonsterIds, Is.EquivalentTo(new short[] { 678, 679, 680 }));
        });
    }

    /// <summary>Verifies active-day filtering separates Acheron Guardian and Wednesday Arca Battle days.</summary>
    [Test]
    public void ScheduleResolutionRespectsActiveDays()
    {
        // Wednesday 22:00 UTC
        var wed22Utc = new DateTime(2026, 11, 25, 22, 0, 0, DateTimeKind.Utc);
        Assert.Multiple(() =>
        {
            Assert.That(SeasonNineEventsEngine.ResolveDueScheduleSlot(["22:00"], "UTC", wed22Utc, "Sun,Mon,Tue,Thu,Fri,Sat"), Is.Null);
            Assert.That(SeasonNineEventsEngine.ResolveDueScheduleSlot(["22:00"], "UTC", wed22Utc, "Wed"), Is.EqualTo("2026-11-25 22:00"));
        });
    }

    /// <summary>Verifies tournament stage resolution and gift item numbers for Chaos Castle Survival and Tormented Square.</summary>
    [Test]
    public void TournamentStageAndRewardsResolveCorrectly()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SeasonNineEventsEngine.ResolveTournamentStage(16, "16,17,18", "19,20", 21), Is.EqualTo(SeasonNineEventsEngine.TournamentStage.Elimination));
            Assert.That(SeasonNineEventsEngine.ResolveTournamentStage(20, "16,17,18", "19,20", 21), Is.EqualTo(SeasonNineEventsEngine.TournamentStage.SemiFinal));
            Assert.That(SeasonNineEventsEngine.ResolveTournamentStage(21, "16,17,18", "19,20", 21), Is.EqualTo(SeasonNineEventsEngine.TournamentStage.Final));
            Assert.That(SeasonNineEventsEngine.ResolveTournamentStage(24, "16,17,18", "19,20", 21), Is.EqualTo(SeasonNineEventsEngine.TournamentStage.Practice));
            Assert.That(SeasonNineEventsEngine.ResolveChaosCastleSurvivalGiftNumber(SeasonNineEventsEngine.TournamentStage.Elimination), Is.EqualTo(254));
            Assert.That(SeasonNineEventsEngine.ResolveChaosCastleSurvivalGiftNumber(SeasonNineEventsEngine.TournamentStage.SemiFinal), Is.EqualTo(256));
            Assert.That(SeasonNineEventsEngine.ResolveChaosCastleSurvivalGiftNumber(SeasonNineEventsEngine.TournamentStage.Final), Is.EqualTo(257));
            Assert.That(SeasonNineEventsEngine.IsInRewardDayRange(24, 24, 25), Is.True);
            Assert.That(SeasonNineEventsEngine.IsInRewardDayRange(26, 24, 25), Is.False);
        });
    }

    /// <summary>Verifies Arca Battle Trophy of Battle booty mix rate calculation and persistent slot deduplication.</summary>
    [Test]
    public void TrophyMixRateAndStoreSlotDeduplicationWork()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SeasonNineEventsEngine.CalculateTrophyOfBattleMixRate(1, 55, 50, 45, 5), Is.EqualTo(59));
            Assert.That(SeasonNineEventsEngine.CalculateTrophyOfBattleMixRate(50, 55, 50, 45, 5), Is.EqualTo(100));
        });

        var path = Path.Combine(Path.GetTempPath(), $"hnmo-s9e-test-{Guid.NewGuid():N}.json");
        try
        {
            var store = SeasonNineEventsStore.Open(path);
            Assert.That(store.TryClaimScheduleSlot("acheron", "2026-11-26 23:00"), Is.True);
            Assert.That(store.TryClaimScheduleSlot("acheron", "2026-11-26 23:00"), Is.False);
            Assert.That(store.TryClaimScheduleSlot("arca", "2026-11-26 23:00"), Is.True);

            var utc = new DateTime(2026, 11, 26, 10, 0, 0, DateTimeKind.Utc);
            Assert.That(store.TryClaimUrukGuardCooldown(utc, 300), Is.True);
            Assert.That(store.TryClaimUrukGuardCooldown(utc.AddSeconds(120), 300), Is.False);
            Assert.That(store.TryClaimUrukGuardCooldown(utc.AddSeconds(305), 300), Is.True);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
