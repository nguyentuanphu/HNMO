// <copyright file="AntiCheatTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using HNMO.GameLogic.PlugIns.AntiCheat;

/// <summary>
/// Unit tests for <see cref="AntiCheatEngine"/> and <see cref="AntiCheatAuditStore"/>.
/// </summary>
[TestFixture]
public class AntiCheatTests
{
    private string _statePath = string.Empty;

    /// <summary>Sets up an isolated temp state file.</summary>
    [SetUp]
    public void SetUp()
    {
        this._statePath = Path.Combine(Path.GetTempPath(), $"hnmo-ac-{Guid.NewGuid():N}.json");
    }

    /// <summary>Deletes temp state file.</summary>
    [TearDown]
    public void TearDown()
    {
        if (File.Exists(this._statePath))
        {
            File.Delete(this._statePath);
        }
    }

    /// <summary>Verifies IP normalization, loopback exemption, and connection cap enforcement.</summary>
    [Test]
    public void EnforcesPerIpCapAndLoopbackExemption()
    {
        var cfg = new AntiCheatConfiguration
        {
            Enabled = true,
            MaxConnectionsPerIp = 2,
            ExemptLoopbackFromIpLimit = true,
        };

        Assert.That(AntiCheatEngine.NormalizeIp("::ffff:203.0.113.10"), Is.EqualTo("203.0.113.10"));
        Assert.That(AntiCheatEngine.NormalizeIp("203.0.113.10:55901"), Is.EqualTo("203.0.113.10"));

        Assert.That(AntiCheatEngine.ExceedsIpLimit(cfg, "127.0.0.1", 10), Is.False);
        Assert.That(AntiCheatEngine.ExceedsIpLimit(cfg, "203.0.113.10", 1), Is.False);
        Assert.That(AntiCheatEngine.ExceedsIpLimit(cfg, "203.0.113.10", 2), Is.True);

        cfg.Enabled = false;
        Assert.That(AntiCheatEngine.ExceedsIpLimit(cfg, "203.0.113.10", 5), Is.False);
    }

    /// <summary>Verifies attack cadence validation, agility cap check, strike rolling window, and audit log capping.</summary>
    [Test]
    public void EvaluatesAttackIntervalStrikesAndAuditPersistence()
    {
        var cfg = new AntiCheatConfiguration
        {
            Enabled = true,
            FileName = this._statePath,
            BaseMinAttackIntervalMs = 400,
            MinimumAttackIntervalFloorMs = 80,
            LatencyToleranceFactor = 0.75,
            StrikesBeforeDisconnect = 3,
            StrikeWindowMinutes = 5,
            MaxAuditEntries = 50,
            EnableHitHackAgilityCheck = true,
            HitHackMaxAgility = 30000,
        };

        var strikes = new List<DateTime>();
        var t0 = new DateTime(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);

        // Normal attack interval -> allowed
        var ok = AntiCheatEngine.EvaluateAttack(cfg, elapsedMs: 350, attackSpeedStat: 40, agilityStat: 500, strikes, t0);
        Assert.That(ok.IsViolation, Is.False);

        // Too fast attacks -> 3 strikes trigger disconnect
        var s1 = AntiCheatEngine.EvaluateAttack(cfg, elapsedMs: 15, attackSpeedStat: 40, agilityStat: 500, strikes, t0.AddSeconds(1));
        var s2 = AntiCheatEngine.EvaluateAttack(cfg, elapsedMs: 15, attackSpeedStat: 40, agilityStat: 500, strikes, t0.AddSeconds(2));
        var s3 = AntiCheatEngine.EvaluateAttack(cfg, elapsedMs: 15, attackSpeedStat: 40, agilityStat: 500, strikes, t0.AddSeconds(3));
        Assert.That(s1.IsViolation && !s1.ShouldDisconnect, Is.True);
        Assert.That(s2.IsViolation && !s2.ShouldDisconnect, Is.True);
        Assert.That(s3.IsViolation && s3.ShouldDisconnect, Is.True);

        // Agility above cap -> flagged
        var agiStrikes = new List<DateTime>();
        var agi = AntiCheatEngine.EvaluateAttack(cfg, elapsedMs: 500, attackSpeedStat: 100, agilityStat: 35000, agiStrikes, t0);
        Assert.That(agi.IsViolation, Is.True);
        Assert.That(agi.ViolationType, Is.EqualTo("AgilityCap"));

        var store = AntiCheatAuditStore.Open(this._statePath);
        store.Append(
            new AntiCheatViolationEntry
            {
                OccurredAtUtc = t0,
                Account = "acc1",
                Character = "Hero1",
                IpAddress = "203.0.113.10",
                ViolationType = "AttackRate",
                Details = "Test violation",
                ActionTaken = "Disconnected",
            },
            cfg.MaxAuditEntries);

        var loaded = store.ListRecent();
        Assert.That(loaded, Has.Count.EqualTo(1));
        Assert.That(loaded[0].Character, Is.EqualTo("Hero1"));
    }
}
