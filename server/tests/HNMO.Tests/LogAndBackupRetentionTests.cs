// <copyright file="LogAndBackupRetentionTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using HNMO.Interfaces;
using HNMO.Persistence.EntityFramework;

/// <summary>
/// Tests rolling gzipped server/database logs, day and MB caps, GFS backup retention (7 daily / 4 weekly / 3 monthly),
/// disk usage alerts, and export-before-delete database log cleanup.
/// </summary>
[TestFixture]
public sealed class LogAndBackupRetentionTests
{
    private string _tempDir = null!;
    private string _saveDir = null!;
    private string _logDir = null!;

    /// <summary>Creates an isolated temporary save folder.</summary>
    [SetUp]
    public void SetUp()
    {
        this._tempDir = Path.Combine(Path.GetTempPath(), $"hnmo-p1b-{Guid.NewGuid():N}");
        this._saveDir = Path.Combine(this._tempDir, "save");
        this._logDir = Path.Combine(this._saveDir, "logs");
        Directory.CreateDirectory(this._logDir);
    }

    /// <summary>Cleans up temporary test folders.</summary>
    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(this._tempDir))
        {
            Directory.Delete(this._tempDir, recursive: true);
        }
    }

    /// <summary>
    /// Verifies <see cref="RollingLogWriter"/> rolls by size and Sydney date into <c>.log.gz</c>,
    /// searches across rotated and active files, and enforces both age and total MB caps.
    /// </summary>
    [Test]
    public void RollsServerAndDatabaseLogsGzipAndEnforcesRetention()
    {
        var clock = new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
        using (var writer = new RollingLogWriter(
            this._logDir,
            "server.log",
            maxFileBytes: 300,
            maxDays: 7,
            maxTotalBytes: 4096,
            utcNow: () => clock))
        {
            for (var i = 0; i < 12; i++)
            {
                writer.WriteLine($"line-{i:D2}: player entered Lorencia with extra padding payload 0123456789ABCDEF");
            }

            // Advance one Sydney day to force daily roll.
            clock = clock.AddDays(1);
            writer.WriteLine("next-day-entry");
        }

        var gzFiles = Directory.GetFiles(this._logDir, "server.*.log.gz");
        Assert.That(gzFiles.Length, Is.GreaterThanOrEqualTo(2));

        var matches = RollingLogWriter.SearchLines(this._logDir, "all", "next-day-entry");
        Assert.That(matches.Any(l => l.Contains("next-day-entry", StringComparison.Ordinal)), Is.True);

        var rolledMatches = RollingLogWriter.SearchLines(this._logDir, "all", "line-00:");
        Assert.That(rolledMatches.Count, Is.EqualTo(1));

        // External database.log rotation test.
        var dbLog = Path.Combine(this._logDir, "database.log");
        File.WriteAllText(dbLog, new string('X', 500));
        var rotated = RollingLogWriter.RotateExternalLogIfNeeded(dbLog, maxFileBytes: 300, maxDays: 7, maxTotalBytes: 8192, utcNow: clock);
        Assert.That(rotated, Is.True);
        Assert.That(new FileInfo(dbLog).Length, Is.EqualTo(0));
        Assert.That(Directory.GetFiles(this._logDir, "database.*.log.gz").Length, Is.EqualTo(1));

        // Make one archive older than 7 days and verify cleanup removes it.
        var oldArchive = gzFiles[0];
        File.SetLastWriteTimeUtc(oldArchive, clock.UtcDateTime.AddDays(-10));
        var removed = RollingLogWriter.EnforceRetention(this._logDir, maxDays: 7, maxTotalBytes: 1024 * 1024, utcNow: clock);
        Assert.That(removed, Is.GreaterThanOrEqualTo(1));
        Assert.That(File.Exists(oldArchive), Is.False);
    }

    /// <summary>
    /// Verifies GFS backup retention keeps 7 daily + 4 weekly + 3 monthly backups and deletes older redundant backups.
    /// </summary>
    [Test]
    public void AppliesDailyWeeklyMonthlyBackupRetentionAndCalculatesTotalSize()
    {
        var backupsDir = Path.Combine(this._saveDir, "backups");
        Directory.CreateDirectory(backupsDir);
        var anchor = new DateTimeOffset(2026, 11, 16, 4, 0, 0, TimeSpan.FromHours(11));

        // Create 2 archives on day 0 plus 1 per day across 150 days back (~5 months).
        for (var d = 0; d < 120; d += 3)
        {
            var stamp = anchor.AddDays(-d);
            var p = Path.Combine(backupsDir, $"hnmo-backup-{stamp:yyyyMMdd-HHmmss}.zip");
            File.WriteAllBytes(p, new byte[128]);
            File.SetLastWriteTimeUtc(p, stamp.UtcDateTime);
        }

        // Extra same-day backup should be pruned first beyond daily quota.
        var dupPath = Path.Combine(backupsDir, "hnmo-backup-dup.zip");
        File.WriteAllBytes(dupPath, new byte[128]);
        File.SetLastWriteTimeUtc(dupPath, anchor.UtcDateTime.AddHours(-1));

        var summary = ControlOperations.ApplyBackupRetention(this._saveDir, keepDaily: 7, keepWeekly: 4, keepMonthly: 3);

        Assert.That(summary.RemovedCount, Is.GreaterThan(0));
        Assert.That(summary.TotalKept, Is.LessThanOrEqualTo(14));
        Assert.That(summary.TotalBytes, Is.EqualTo(summary.TotalKept * 128L));
        Assert.That(File.Exists(dupPath), Is.False);
    }

    /// <summary>Verifies backup zip + restore round-trip and disk usage reporting.</summary>
    [Test]
    public void CreatesBackupRestoresSaveAndReportsDiskUsage()
    {
        MaintenanceSettings.Save(this._saveDir, new MaintenanceSettings { MaxLogDays = 21, DbLogRetentionDays = 90, LowDiskWarningMb = 1024 });
        var reloaded = MaintenanceSettings.Load(this._saveDir);
        Assert.That(reloaded.MaxLogDays, Is.EqualTo(21));

        var statePath = Path.Combine(this._saveDir, "hnmo-state.json");
        File.WriteAllText(statePath, "{\"v\":1,\"sections\":{\"missions\":\"ok\"}}");
        var sqlSource = Path.Combine(this._tempDir, "dump.sql");
        File.WriteAllText(sqlSource, "-- SQL DUMP");

        var backup = ControlOperations.CreateBackupZip(this._saveDir, sqlSource);
        Assert.That(backup.FileName, Does.EndWith(".zip"));

        File.WriteAllText(statePath, "{\"corrupted\":true}");
        var extractedSql = ControlOperations.RestoreBackupZip(this._saveDir, backup.FileName);
        Assert.That(extractedSql, Is.Not.Null);
        Assert.That(File.ReadAllText(extractedSql!), Is.EqualTo("-- SQL DUMP"));
        Assert.That(File.ReadAllText(statePath), Does.Contain("\"missions\":\"ok\""));

        var disk = ControlOperations.GetDiskUsage(this._saveDir, lowDiskWarningMb: 1024);
        Assert.That(disk.SaveBytes, Is.GreaterThan(0));
        Assert.That(disk.BackupsBytes, Is.GreaterThan(0));
    }

    /// <summary>
    /// Verifies old chat/audit logs are exported into a readable <c>.json.gz</c> archive BEFORE the delete callback runs.
    /// </summary>
    [Test]
    public void ExportsCompressedArchiveBeforeDeletingOldDbLogs()
    {
        var archiveDir = Path.Combine(this._saveDir, "archives");
        var cutoff = new DateTime(2026, 8, 18, 0, 0, 0, DateTimeKind.Utc);
        var deleted = false;
        var chats = new[] { new { Sender = "Alice", Text = "old message", TimeUtc = cutoff.AddDays(-5) } };
        var audits = new[] { new { Username = "admin", Action = "Save", StartedAtUtc = cutoff.AddDays(-2) } };

        var res = PostgresDbLogRetentionStore.ExportBeforeDelete(
            archiveDir,
            cutoff,
            chats,
            audits,
            () => deleted = true);

        Assert.That(deleted, Is.True);
        Assert.That(res.DeletedChatRows, Is.EqualTo(1));
        Assert.That(res.DeletedAuditRows, Is.EqualTo(1));
        Assert.That(res.ExportArchivePath, Is.Not.Null.And.Exist);

        using var fs = File.OpenRead(res.ExportArchivePath!);
        using var gzip = new GZipStream(fs, CompressionMode.Decompress);
        using var doc = JsonDocument.Parse(gzip);
        Assert.That(doc.RootElement.GetProperty("chatLogs").GetArrayLength(), Is.EqualTo(1));
        Assert.That(doc.RootElement.GetProperty("auditEntries").GetArrayLength(), Is.EqualTo(1));
    }
}
