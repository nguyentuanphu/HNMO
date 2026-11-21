// <copyright file="ControlOperations.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Interfaces;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Shared helper operations for the HNMO Control page (backups, Sydney time formatting,
/// update release parsing, and log tailing).
/// </summary>
public static class ControlOperations
{
    /// <summary>
    /// Formats a UTC timestamp in Sydney local time (<c>yyyy-MM-dd HH:mm:ss</c>).
    /// </summary>
    /// <param name="utcTime">The UTC timestamp.</param>
    /// <returns>The formatted Sydney time string.</returns>
    public static string FormatSydneyTime(DateTimeOffset utcTime)
    {
        var tz = GetSydneyTimeZone();
        var sydney = TimeZoneInfo.ConvertTime(utcTime, tz);
        return sydney.ToString("yyyy-MM-dd HH:mm:ss");
    }

    /// <summary>
    /// Gets a backup file name stamped with current Sydney time (<c>hnmo-backup-yyyyMMdd-HHmmss.zip</c>).
    /// </summary>
    /// <param name="utcNow">Optional UTC timestamp override for tests.</param>
    /// <returns>The backup file name.</returns>
    public static string CreateBackupFileName(DateTimeOffset? utcNow = null)
    {
        var tz = GetSydneyTimeZone();
        var sydney = TimeZoneInfo.ConvertTime(utcNow ?? DateTimeOffset.UtcNow, tz);
        return $"hnmo-backup-{sydney:yyyyMMdd-HHmmss}.zip";
    }

    /// <summary>
    /// Creates a backup <c>.zip</c> in <c>save/backups</c> containing <c>save/</c> JSON/settings files
    /// and an optional SQL database dump.
    /// </summary>
    /// <param name="saveDirectory">The <c>save/</c> directory.</param>
    /// <param name="databaseDumpSqlPath">Optional path to a <c>pg_dump</c> SQL file.</param>
    /// <param name="utcNow">Optional UTC timestamp override for tests.</param>
    /// <returns>The created backup info.</returns>
    public static BackupInfo CreateBackupZip(string saveDirectory, string? databaseDumpSqlPath = null, DateTimeOffset? utcNow = null)
    {
        var backupsDir = Path.Combine(saveDirectory, "backups");
        Directory.CreateDirectory(backupsDir);
        var fileName = CreateBackupFileName(utcNow);
        var zipPath = Path.Combine(backupsDir, fileName);
        var tempZip = zipPath + ".tmp";
        if (File.Exists(tempZip))
        {
            File.Delete(tempZip);
        }

        using (var zip = ZipFile.Open(tempZip, ZipArchiveMode.Create))
        {
            if (Directory.Exists(saveDirectory))
            {
                foreach (var file in Directory.EnumerateFiles(saveDirectory, "*", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(saveDirectory, file).Replace('\\', '/');
                    if (IsLeftOutOfBackup(rel))
                    {
                        continue;
                    }

                    zip.CreateEntryFromFile(file, $"save/{rel}", CompressionLevel.Optimal);
                }
            }

            if (!string.IsNullOrWhiteSpace(databaseDumpSqlPath) && File.Exists(databaseDumpSqlPath))
            {
                zip.CreateEntryFromFile(databaseDumpSqlPath, "database.sql", CompressionLevel.Optimal);
            }
        }

        File.Move(tempZip, zipPath, overwrite: true);
        var info = new FileInfo(zipPath);
        return new BackupInfo(fileName, info.Length, FormatSydneyTime(utcNow ?? DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Whether a file of the <c>save/</c> folder stays out of a backup (and is never written back by a restore): the
    /// backups themselves, the logs, the database files (the backup holds a dump instead), the updater's downloads and
    /// helper (<c>updates/</c>), process ids and unfinished files.
    /// </summary>
    /// <param name="relativePath">The path inside <c>save/</c>, with forward slashes.</param>
    /// <returns><c>true</c> if the file is left out.</returns>
    public static bool IsLeftOutOfBackup(string relativePath)
    {
        var rel = relativePath.Replace('\\', '/');
        return rel.StartsWith("backups/", StringComparison.OrdinalIgnoreCase)
            || rel.StartsWith("logs/", StringComparison.OrdinalIgnoreCase)
            || rel.StartsWith("database/", StringComparison.OrdinalIgnoreCase)
            || rel.StartsWith("updates/", StringComparison.OrdinalIgnoreCase)
            || rel.EndsWith(".pid", StringComparison.OrdinalIgnoreCase)
            || rel.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Lists backup archives in <c>save/backups</c>, newest first.
    /// </summary>
    /// <param name="saveDirectory">The <c>save/</c> directory.</param>
    /// <returns>The list of backups.</returns>
    public static IReadOnlyList<BackupInfo> ListBackups(string saveDirectory)
    {
        var backupsDir = Path.Combine(saveDirectory, "backups");
        if (!Directory.Exists(backupsDir))
        {
            return Array.Empty<BackupInfo>();
        }

        return Directory.GetFiles(backupsDir, "*.zip", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Select(f => new BackupInfo(f.Name, f.Length, FormatSydneyTime(new DateTimeOffset(f.LastWriteTimeUtc, TimeSpan.Zero))))
            .ToArray();
    }

    /// <summary>
    /// Restores <c>save/</c> files from a backup archive and returns the path to an extracted
    /// <c>database.sql</c> file if one was included.
    /// </summary>
    /// <param name="saveDirectory">The <c>save/</c> directory.</param>
    /// <param name="backupFileName">The backup archive file name inside <c>save/backups</c>.</param>
    /// <returns>Path to extracted <c>database.sql</c>, or <c>null</c> if none.</returns>
    public static string? RestoreBackupZip(string saveDirectory, string backupFileName)
    {
        if (string.IsNullOrWhiteSpace(backupFileName)
            || backupFileName.Contains('/')
            || backupFileName.Contains('\\')
            || backupFileName.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException("Invalid backup file name.", nameof(backupFileName));
        }

        var zipPath = Path.Combine(saveDirectory, "backups", backupFileName);
        if (!File.Exists(zipPath))
        {
            throw new FileNotFoundException("Backup archive not found.", zipPath);
        }

        string? restoredSqlPath = null;
        var fullSaveDir = Path.GetFullPath(saveDirectory);
        using var zip = ZipFile.OpenRead(zipPath);
        foreach (var entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            if (entry.FullName.Equals("database.sql", StringComparison.OrdinalIgnoreCase))
            {
                restoredSqlPath = Path.Combine(fullSaveDir, "restore-database.sql");
                entry.ExtractToFile(restoredSqlPath, overwrite: true);
                continue;
            }

            if (entry.FullName.StartsWith("save/", StringComparison.OrdinalIgnoreCase))
            {
                var rel = entry.FullName["save/".Length..];
                if (IsLeftOutOfBackup(rel))
                {
                    // Older backups held the updater's folder; restoring it would overwrite the running update helper.
                    continue;
                }

                var dest = Path.GetFullPath(Path.Combine(fullSaveDir, rel));
                if (!dest.StartsWith(fullSaveDir + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                {
                    continue;
                }

                var dir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                entry.ExtractToFile(dest, overwrite: true);
            }
        }

        return restoredSqlPath;
    }

    /// <summary>
    /// Parses the newest release's details (from the developer's hub, <see cref="DeveloperHubAddress.LatestRelease"/>)
    /// and compares it to the local build stamp.
    /// </summary>
    /// <param name="releaseJson">Raw JSON: tag_name, published_at, body, html_url (the download link).</param>
    /// <param name="localBuildStamp">Local <c>.hnmo-build</c> stamp, if present.</param>
    /// <returns>Parsed update check result.</returns>
    public static UpdateCheckResult ParseRelease(string releaseJson, string? localBuildStamp)
    {
        using var doc = JsonDocument.Parse(releaseJson);
        var root = doc.RootElement;
        var tag = root.TryGetProperty("tag_name", out var t) ? (t.GetString() ?? "latest") : "latest";
        var body = root.TryGetProperty("body", out var b) ? (b.GetString() ?? string.Empty) : string.Empty;
        var htmlUrl = root.TryGetProperty("html_url", out var u) ? (u.GetString() ?? string.Empty) : string.Empty;
        var published = string.Empty;
        var isNewer = false;

        if (root.TryGetProperty("published_at", out var p) && DateTimeOffset.TryParse(p.GetString(), out var publishedUtc))
        {
            published = FormatSydneyTime(publishedUtc);
            if (!string.IsNullOrWhiteSpace(localBuildStamp))
            {
                var parts = localBuildStamp.Trim().Split('-');
                var datePart = parts.LastOrDefault();
                if (datePart is { Length: 8 } && DateOnly.TryParseExact(datePart, "yyyyMMdd", out var localDate))
                {
                    var localUtc = new DateTimeOffset(localDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
                    isNewer = publishedUtc > localUtc.AddDays(1);
                }
            }
            else
            {
                isNewer = true;
            }
        }

        return new UpdateCheckResult(tag, published, body, htmlUrl, isNewer);
    }

    /// <summary>
    /// Reads the last <paramref name="maxLines"/> lines from a log file safely while it is being written.
    /// </summary>
    /// <param name="logFilePath">The log file path.</param>
    /// <param name="maxLines">Maximum number of trailing lines to return.</param>
    /// <returns>The trailing log lines.</returns>
    public static IReadOnlyList<string> ReadTailLines(string logFilePath, int maxLines = 120)
    {
        if (!File.Exists(logFilePath))
        {
            return Array.Empty<string>();
        }

        var lines = new Queue<string>(maxLines);
        using var stream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
        {
            if (lines.Count == maxLines)
            {
                lines.Dequeue();
            }

            lines.Enqueue(line);
        }

        return lines.ToArray();
    }

    /// <summary>
    /// Enforces daily/weekly/monthly retention across <c>save/backups/*.zip</c> (keeping the newest backup per
    /// calendar day, ISO week, and month up to the configured limits) and returns the remaining total size.
    /// </summary>
    /// <param name="saveDirectory">The <c>save/</c> directory.</param>
    /// <param name="keepDaily">Daily backups to keep (default 7).</param>
    /// <param name="keepWeekly">Weekly backups to keep (default 4).</param>
    /// <param name="keepMonthly">Monthly backups to keep (default 3).</param>
    /// <returns>Summary of kept and removed archives.</returns>
    public static BackupRetentionSummary ApplyBackupRetention(
        string saveDirectory,
        int keepDaily = 7,
        int keepWeekly = 4,
        int keepMonthly = 3)
    {
        var backupsDir = Path.Combine(saveDirectory, "backups");
        if (!Directory.Exists(backupsDir))
        {
            return new BackupRetentionSummary(0, 0, 0);
        }

        var files = Directory.GetFiles(backupsDir, "*.zip", SearchOption.TopDirectoryOnly)
            .Select(p => new FileInfo(p))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .ToList();
        if (files.Count == 0)
        {
            return new BackupRetentionSummary(0, 0, 0);
        }

        var tz = GetSydneyTimeZone();
        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenDays = new HashSet<DateOnly>();
        var seenWeeks = new HashSet<(int Year, int Week)>();
        var seenMonths = new HashSet<(int Year, int Month)>();
        var keptDays = 0;
        var keptWeeks = 0;
        var keptMonths = 0;

        foreach (var file in files)
        {
            var local = TimeZoneInfo.ConvertTime(new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero), tz).DateTime;
            var day = DateOnly.FromDateTime(local);
            var week = (System.Globalization.ISOWeek.GetYear(local), System.Globalization.ISOWeek.GetWeekOfYear(local));
            var month = (local.Year, local.Month);

            var isNewDay = seenDays.Add(day);
            var isNewWeek = seenWeeks.Add(week);
            var isNewMonth = seenMonths.Add(month);

            var selected = false;
            if (isNewDay && keptDays < Math.Max(1, keepDaily))
            {
                keptDays++;
                selected = true;
            }

            if (isNewWeek && keptWeeks < keepWeekly)
            {
                keptWeeks++;
                selected = true;
            }

            if (isNewMonth && keptMonths < keepMonthly)
            {
                keptMonths++;
                selected = true;
            }

            if (selected)
            {
                keep.Add(file.FullName);
            }
        }

        var removed = 0;
        long totalBytes = 0;
        foreach (var file in files)
        {
            if (!keep.Contains(file.FullName))
            {
                try
                {
                    file.Delete();
                    removed++;
                }
                catch (IOException)
                {
                    totalBytes += file.Length;
                }
            }
            else
            {
                totalBytes += file.Length;
            }
        }

        return new BackupRetentionSummary(keep.Count, totalBytes, removed);
    }

    /// <summary>Computes drive free space and save/logs/backups/database sizes.</summary>
    /// <param name="saveDirectory">The <c>save/</c> directory.</param>
    /// <param name="lowDiskWarningMb">Threshold (MB) below which <see cref="DiskUsageReport.IsLowDisk"/> is true.</param>
    /// <returns>Disk usage report.</returns>
    public static DiskUsageReport GetDiskUsage(string saveDirectory, int lowDiskWarningMb = 2048)
    {
        var fullSave = Path.GetFullPath(saveDirectory);
        var root = Path.GetPathRoot(fullSave) ?? fullSave;
        long totalDrive = 0;
        long freeDrive = 0;
        try
        {
            var drive = new DriveInfo(root);
            if (drive.IsReady)
            {
                totalDrive = drive.TotalSize;
                freeDrive = drive.AvailableFreeSpace;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
        }

        var saveBytes = SumDirectoryBytes(fullSave);
        var logsBytes = SumDirectoryBytes(Path.Combine(fullSave, "logs"));
        var backupsBytes = SumDirectoryBytes(Path.Combine(fullSave, "backups"));
        var databaseBytes = SumDirectoryBytes(Path.Combine(fullSave, "database"));
        var thresholdBytes = Math.Max(1L, lowDiskWarningMb) * 1024L * 1024L;
        var isLow = totalDrive > 0 && (freeDrive < thresholdBytes || (freeDrive * 20L) < totalDrive);
        return new DiskUsageReport(root, totalDrive, freeDrive, saveBytes, logsBytes, backupsBytes, databaseBytes, lowDiskWarningMb, isLow);
    }

    private static long SumDirectoryBytes(string path)
    {
        if (!Directory.Exists(path))
        {
            return 0;
        }

        long sum = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    sum += new FileInfo(file).Length;
                }
                catch (IOException)
                {
                }
            }
        }
        catch (IOException)
        {
        }

        return sum;
    }

    private static TimeZoneInfo GetSydneyTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.Utc;
            }
        }
    }
}
