// <copyright file="CrashWatchPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.IO;
using HNMO.Interfaces;
using HNMO.Web.AdminPanel.Pages;

/// <summary>
/// Integration checks for <see cref="CrashWatch"/> settings and incident persistence.
/// </summary>
[TestFixture]
public class CrashWatchPageTests
{
    /// <summary>Verifies <see cref="CrashWatchStore"/> and <see cref="MaintenanceSettings"/> cooperate for admin notices.</summary>
    [Test]
    public void PersistsCrashWatchIncidentsAndAcknowledgmentState()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"hnmo-cw-page-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var settings = new MaintenanceSettings
            {
                AutoRestartOnCrash = false,
                MaxCrashRestartsPerHour = 4,
                CrashRestartDelaySeconds = 7,
            };
            MaintenanceSettings.Save(tempDir, settings);

            var reloaded = MaintenanceSettings.Load(tempDir);
            Assert.That(reloaded.AutoRestartOnCrash, Is.False);
            Assert.That(reloaded.MaxCrashRestartsPerHour, Is.EqualTo(4));
            Assert.That(reloaded.CrashRestartDelaySeconds, Is.EqualTo(7));

            CrashWatchStore.RecordIncident(tempDir, new CrashIncident
            {
                Id = "inc1",
                OccurredAtUtc = DateTimeOffset.UtcNow,
                OccurredAtSydney = ControlOperations.FormatSydneyTime(DateTimeOffset.UtcNow),
                ExitCode = 1,
                AutoRestarted = false,
                SummaryEn = "Unexpected exit (1)",
                SummaryVi = "Thoát bất thường (1)",
                TailExcerpt = "Fatal error line",
                Acknowledged = false,
            });

            var unread = CrashWatchStore.ListIncidents(tempDir).Count(i => !i.Acknowledged);
            Assert.That(unread, Is.EqualTo(1));
            CrashWatchStore.AcknowledgeAll(tempDir);
            Assert.That(CrashWatchStore.ListIncidents(tempDir).Count(i => !i.Acknowledged), Is.Zero);
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
