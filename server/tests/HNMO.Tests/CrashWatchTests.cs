// <copyright file="CrashWatchTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using HNMO.Interfaces;

/// <summary>
/// Unit tests for <see cref="CrashWatchPolicy"/>, <see cref="CrashWatchStore"/>, and <see cref="MaintenanceSettings"/>.
/// </summary>
[TestFixture]
public class CrashWatchTests
{
    /// <summary>Verifies intentional stop and restart exit codes never trigger crash alerts or backoff restarts.</summary>
    [Test]
    public void IgnoresIntentionalStopsAndConfiguredRestartExitCode()
    {
        var settings = new MaintenanceSettings();
        var now = new DateTimeOffset(2026, 11, 17, 1, 0, 0, TimeSpan.Zero);

        var stopDecision = CrashWatchPolicy.Evaluate(settings, exitCode: 1, intentionalStop: true, [], now);
        Assert.That(stopDecision.IsCrash, Is.False);
        Assert.That(stopDecision.ShouldRestart, Is.False);

        var hostRestartDecision = CrashWatchPolicy.Evaluate(
            settings,
            exitCode: HostSettingsStore.RestartExitCode,
            intentionalStop: false,
            [],
            now);
        Assert.That(hostRestartDecision.IsCrash, Is.False);
        Assert.That(hostRestartDecision.ShouldRestart, Is.False);
    }

    /// <summary>Verifies escalating backoff and sliding 60-minute restart cap.</summary>
    [Test]
    public void EnforcesSlidingHourlyCapAndEscalatingBackoff()
    {
        var settings = new MaintenanceSettings
        {
            AutoRestartOnCrash = true,
            MaxCrashRestartsPerHour = 3,
            CrashRestartDelaySeconds = 4,
        };
        var now = new DateTimeOffset(2026, 11, 17, 2, 0, 0, TimeSpan.Zero);

        var d1 = CrashWatchPolicy.Evaluate(settings, 137, intentionalStop: false, [], now);
        Assert.That(d1.IsCrash, Is.True);
        Assert.That(d1.ShouldRestart, Is.True);
        Assert.That(d1.DelaySeconds, Is.EqualTo(4));

        var priors = new List<DateTimeOffset>
        {
            now.AddMinutes(-20),
            now.AddMinutes(-10),
        };
        var d3 = CrashWatchPolicy.Evaluate(settings, -1, intentionalStop: false, priors, now);
        Assert.That(d3.ShouldRestart, Is.True);
        Assert.That(d3.DelaySeconds, Is.EqualTo(12));

        priors.Add(now.AddMinutes(-2));
        var capped = CrashWatchPolicy.Evaluate(settings, -1, intentionalStop: false, priors, now);
        Assert.That(capped.IsCrash, Is.True);
        Assert.That(capped.ShouldRestart, Is.False);
        Assert.That(capped.SummaryEn, Does.Contain("manual check"));
        Assert.That(capped.SummaryVi, Does.Contain("thủ công"));
    }

    /// <summary>Verifies crash incidents and crash watch settings persist cleanly and acknowledge clears unread count.</summary>
    [Test]
    public void StoreAndMaintenanceSettingsRoundTripAndAcknowledgeCleanly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"hnmo-crashwatch-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            MaintenanceSettings.Save(tempDir, new MaintenanceSettings
            {
                AutoRestartOnCrash = true,
                MaxCrashRestartsPerHour = 8,
                CrashRestartDelaySeconds = 5,
                CrashDiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
            });

            var loaded = MaintenanceSettings.Load(tempDir);
            Assert.That(loaded.AutoRestartOnCrash, Is.True);
            Assert.That(loaded.MaxCrashRestartsPerHour, Is.EqualTo(8));
            Assert.That(loaded.CrashRestartDelaySeconds, Is.EqualTo(5));
            Assert.That(loaded.CrashDiscordWebhookUrl, Is.EqualTo("https://discord.com/api/webhooks/123/abc"));

            CrashWatchStore.RecordIncident(tempDir, new CrashIncident
            {
                Id = "c1",
                OccurredAtUtc = DateTimeOffset.UtcNow,
                OccurredAtSydney = "2026-11-17 11:00:00",
                ExitCode = 139,
                AutoRestarted = true,
                DelaySeconds = 5,
                SummaryEn = "Crashed",
                SummaryVi = "Sự cố",
                Acknowledged = false,
            });

            var list = CrashWatchStore.ListIncidents(tempDir);
            Assert.That(list.Count, Is.EqualTo(1));
            Assert.That(list[0].Acknowledged, Is.False);

            var acked = CrashWatchStore.AcknowledgeAll(tempDir);
            Assert.That(acked, Is.EqualTo(1));
            Assert.That(CrashWatchStore.ListIncidents(tempDir)[0].Acknowledged, Is.True);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
