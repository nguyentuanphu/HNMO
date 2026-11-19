// <copyright file="RollingLogWriter.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Interfaces;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

/// <summary>
/// Writes lines to an active log file (e.g. <c>save/logs/server.log</c>), rolling daily (Sydney date)
/// or once the active file reaches <see cref="MaxFileBytes"/>, compressing closed segments to <c>.log.gz</c>,
/// and enforcing day/size retention caps across <c>save/logs</c>.
/// </summary>
public sealed class RollingLogWriter : IDisposable
{
    private readonly object _sync = new();

    private readonly string _logDirectory;

    private readonly string _baseName;

    private readonly string _activeFilePath;

    private readonly Func<DateTimeOffset> _utcNow;

    private DateOnly _currentSydneyDay;

    private StreamWriter _writer;

    private long _currentBytes;

    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RollingLogWriter"/> class.
    /// </summary>
    /// <param name="logDirectory">Log directory (e.g. <c>save/logs</c>).</param>
    /// <param name="baseFileName">Active file name (e.g. <c>server.log</c>).</param>
    /// <param name="maxFileBytes">Roll file once its byte size reaches this threshold.</param>
    /// <param name="maxDays">Maximum age of rotated <c>.gz</c> logs in days.</param>
    /// <param name="maxTotalBytes">Maximum combined bytes of rotated <c>.gz</c> and active logs in <paramref name="logDirectory"/>.</param>
    /// <param name="utcNow">Optional clock override for tests.</param>
    public RollingLogWriter(
        string logDirectory,
        string baseFileName = "server.log",
        long maxFileBytes = 10L * 1024 * 1024,
        int maxDays = 14,
        long maxTotalBytes = 200L * 1024 * 1024,
        Func<DateTimeOffset>? utcNow = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseFileName);
        Directory.CreateDirectory(logDirectory);

        this._logDirectory = Path.GetFullPath(logDirectory);
        this._baseName = Path.GetFileNameWithoutExtension(baseFileName);
        this._activeFilePath = Path.Combine(this._logDirectory, baseFileName);
        this.MaxFileBytes = Math.Max(256, maxFileBytes);
        this.MaxDays = Math.Max(1, maxDays);
        this.MaxTotalBytes = Math.Max(1024, maxTotalBytes);
        this._utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        this._currentSydneyDay = GetSydneyDate(this._utcNow());

        // Rotate a leftover file from an earlier Sydney day before reopening.
        if (File.Exists(this._activeFilePath))
        {
            var info = new FileInfo(this._activeFilePath);
            var existingDay = GetSydneyDate(new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero));
            if (existingDay < this._currentSydneyDay && info.Length > 0)
            {
                CompressFileToArchive(this._activeFilePath, this._logDirectory, this._baseName, existingDay);
            }
        }

        this._writer = OpenAppendWriter(this._activeFilePath, out this._currentBytes);
    }

    /// <summary>Gets or sets maximum bytes before an active log rolls to <c>.log.gz</c>.</summary>
    public long MaxFileBytes { get; set; }

    /// <summary>Gets or sets maximum retention days for archived <c>.log.gz</c> files.</summary>
    public int MaxDays { get; set; }

    /// <summary>Gets or sets maximum combined bytes for <c>save/logs</c>.</summary>
    public long MaxTotalBytes { get; set; }

    /// <summary>Gets the active log file path.</summary>
    public string ActiveFilePath => this._activeFilePath;

    /// <summary>
    /// Safely rolls an external log file (such as PostgreSQL's <c>database.log</c>) if it exceeds
    /// <paramref name="maxFileBytes"/> or was last modified on an earlier Sydney day, gzipping the rolled copy.
    /// </summary>
    /// <param name="logFilePath">Full path to the log file.</param>
    /// <param name="maxFileBytes">Size threshold in bytes.</param>
    /// <param name="maxDays">Age cap for archives.</param>
    /// <param name="maxTotalBytes">Folder cap in bytes.</param>
    /// <param name="utcNow">Optional UTC clock.</param>
    /// <returns><c>true</c> if rotated; otherwise <c>false</c>.</returns>
    public static bool RotateExternalLogIfNeeded(
        string logFilePath,
        long maxFileBytes,
        int maxDays,
        long maxTotalBytes,
        DateTimeOffset? utcNow = null)
    {
        if (!File.Exists(logFilePath))
        {
            return false;
        }

        var now = utcNow ?? DateTimeOffset.UtcNow;
        var info = new FileInfo(logFilePath);
        if (info.Length == 0)
        {
            return false;
        }

        var today = GetSydneyDate(now);
        var fileDay = GetSydneyDate(new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero));
        if (info.Length < Math.Max(256, maxFileBytes) && fileDay >= today)
        {
            return false;
        }

        var dir = Path.GetDirectoryName(logFilePath)!;
        var baseName = Path.GetFileNameWithoutExtension(logFilePath);
        var archivePath = BuildArchivePath(dir, baseName, fileDay);
        var tempGz = archivePath + ".tmp";
        try
        {
            using (var src = new FileStream(logFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                using (var dst = new FileStream(tempGz, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var gzip = new GZipStream(dst, CompressionLevel.Optimal))
                {
                    src.CopyTo(gzip);
                }

                src.SetLength(0);
                src.Flush();
            }

            File.Move(tempGz, archivePath, overwrite: true);
            EnforceRetention(dir, maxDays, maxTotalBytes, now);
            return true;
        }
        catch (IOException)
        {
            if (File.Exists(tempGz))
            {
                File.Delete(tempGz);
            }

            return false;
        }
    }

    /// <summary>
    /// Deletes rotated <c>.log.gz</c> archives older than <paramref name="maxDays"/> or exceeding <paramref name="maxTotalBytes"/>.
    /// </summary>
    /// <param name="logDirectory">Log directory.</param>
    /// <param name="maxDays">Max age in days.</param>
    /// <param name="maxTotalBytes">Max total bytes in <paramref name="logDirectory"/>.</param>
    /// <param name="utcNow">Optional UTC clock.</param>
    /// <returns>Number of removed archives.</returns>
    public static int EnforceRetention(string logDirectory, int maxDays, long maxTotalBytes, DateTimeOffset? utcNow = null)
    {
        if (!Directory.Exists(logDirectory))
        {
            return 0;
        }

        var now = (utcNow ?? DateTimeOffset.UtcNow).UtcDateTime;
        var cutoff = now.AddDays(-Math.Max(1, maxDays));
        var removed = 0;

        var archives = Directory.GetFiles(logDirectory, "*.log.gz", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderBy(f => f.LastWriteTimeUtc)
            .ToList();

        foreach (var f in archives.Where(f => f.LastWriteTimeUtc < cutoff).ToList())
        {
            TryDeleteFile(f.FullName);
            archives.Remove(f);
            removed++;
        }

        long activeBytes = Directory.GetFiles(logDirectory, "*.log", SearchOption.TopDirectoryOnly).Sum(p => new FileInfo(p).Length);
        long archiveBytes = archives.Sum(f => f.Length);
        var cap = Math.Max(1024, maxTotalBytes);
        while (archives.Count > 0 && activeBytes + archiveBytes > cap)
        {
            var oldest = archives[0];
            archives.RemoveAt(0);
            archiveBytes -= oldest.Length;
            TryDeleteFile(oldest.FullName);
            removed++;
        }

        return removed;
    }

    /// <summary>Lists active <c>.log</c> and rotated <c>.log.gz</c> files in <paramref name="logDirectory"/>.</summary>
    /// <param name="logDirectory">Log folder.</param>
    /// <returns>Sorted file metadata list.</returns>
    public static IReadOnlyList<LogFileEntry> ListLogFiles(string logDirectory)
    {
        if (!Directory.Exists(logDirectory))
        {
            return Array.Empty<LogFileEntry>();
        }

        return Directory.EnumerateFiles(logDirectory, "*", SearchOption.TopDirectoryOnly)
            .Where(p => p.EndsWith(".log", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".log.gz", StringComparison.OrdinalIgnoreCase))
            .Select(p => new FileInfo(p))
            .OrderBy(f => f.Name.EndsWith(".log", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenByDescending(f => f.LastWriteTimeUtc)
            .Select(f => new LogFileEntry(
                f.Name,
                f.Length,
                ControlOperations.FormatSydneyTime(new DateTimeOffset(f.LastWriteTimeUtc, TimeSpan.Zero)),
                f.Name.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }

    /// <summary>Searches or tails lines from a single log file (active or gzipped) or across all log files.</summary>
    /// <param name="logDirectory">Log folder.</param>
    /// <param name="fileName">File name or <c>null</c>/<c>all</c> for all logs.</param>
    /// <param name="query">Optional case-insensitive substring filter.</param>
    /// <param name="maxLines">Maximum trailing lines to return.</param>
    /// <returns>Matching lines.</returns>
    public static IReadOnlyList<string> SearchLines(string logDirectory, string? fileName, string? query, int maxLines = 200)
    {
        if (!Directory.Exists(logDirectory))
        {
            return Array.Empty<string>();
        }

        maxLines = Math.Clamp(maxLines, 1, 2000);
        IEnumerable<string> targetFiles;
        if (!string.IsNullOrWhiteSpace(fileName) && !string.Equals(fileName, "all", StringComparison.OrdinalIgnoreCase))
        {
            if (fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains("..", StringComparison.Ordinal))
            {
                return Array.Empty<string>();
            }

            var full = Path.Combine(logDirectory, fileName);
            if (!File.Exists(full))
            {
                return Array.Empty<string>();
            }

            targetFiles = [full];
        }
        else
        {
            // Oldest rotated first, then active logs so trailing output ends with newest lines.
            var gz = Directory.GetFiles(logDirectory, "*.log.gz", SearchOption.TopDirectoryOnly)
                .OrderBy(f => new FileInfo(f).LastWriteTimeUtc);
            var active = Directory.GetFiles(logDirectory, "*.log", SearchOption.TopDirectoryOnly)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
            targetFiles = gz.Concat(active);
        }

        var queue = new Queue<string>(maxLines);
        var hasQuery = !string.IsNullOrWhiteSpace(query);
        foreach (var path in targetFiles)
        {
            try
            {
                using var raw = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using Stream stream = path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
                    ? new GZipStream(raw, CompressionMode.Decompress)
                    : raw;
                using var reader = new StreamReader(stream);
                var shortName = Path.GetFileName(path);
                while (reader.ReadLine() is { } line)
                {
                    if (hasQuery && !line.Contains(query!, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (queue.Count == maxLines)
                    {
                        queue.Dequeue();
                    }

                    queue.Enqueue(fileName is null || string.Equals(fileName, "all", StringComparison.OrdinalIgnoreCase)
                        ? $"[{shortName}] {line}"
                        : line);
                }
            }
            catch (IOException)
            {
                // Skip file if concurrently deleted during retention cleanup.
            }
            catch (InvalidDataException)
            {
                // Skip partial gzip if mid-rotation.
            }
        }

        return queue.ToArray();
    }

    /// <summary>Writes one line, rolling the active file first if the Sydney date changed or size reached <see cref="MaxFileBytes"/>.</summary>
    /// <param name="line">Line text to append.</param>
    public void WriteLine(string line)
    {
        lock (this._sync)
        {
            if (this._disposed)
            {
                return;
            }

            var now = this._utcNow();
            var today = GetSydneyDate(now);
            if (this._currentBytes > 0 && (today > this._currentSydneyDay || this._currentBytes >= this.MaxFileBytes))
            {
                var archiveDay = today > this._currentSydneyDay ? this._currentSydneyDay : today;
                this.RollInternal(archiveDay, now);
                this._currentSydneyDay = today;
            }
            else if (today > this._currentSydneyDay)
            {
                this._currentSydneyDay = today;
            }

            this._writer.WriteLine(line);
            this._currentBytes += Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (this._sync)
        {
            if (this._disposed)
            {
                return;
            }

            this._disposed = true;
            this._writer.Dispose();
        }
    }

    private static DateOnly GetSydneyDate(DateTimeOffset utc)
    {
        var formatted = ControlOperations.FormatSydneyTime(utc);
        return DateOnly.ParseExact(formatted[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static StreamWriter OpenAppendWriter(string path, out long existingBytes)
    {
        var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
        stream.Seek(0, SeekOrigin.End);
        existingBytes = stream.Length;
        return new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = true };
    }

    private static string BuildArchivePath(string logDirectory, string baseName, DateOnly day)
    {
        var dateStamp = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        for (var seq = 1; ; seq++)
        {
            var candidate = Path.Combine(logDirectory, $"{baseName}.{dateStamp}.{seq}.log.gz");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    private static void CompressFileToArchive(string activePath, string logDirectory, string baseName, DateOnly day)
    {
        var archivePath = BuildArchivePath(logDirectory, baseName, day);
        var tempPath = archivePath + ".tmp";
        using (var src = new FileStream(activePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var dst = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var gzip = new GZipStream(dst, CompressionLevel.Optimal))
        {
            src.CopyTo(gzip);
        }

        File.Move(tempPath, archivePath, overwrite: true);
        File.Delete(activePath);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
    }

    private void RollInternal(DateOnly archiveDay, DateTimeOffset now)
    {
        this._writer.Dispose();
        CompressFileToArchive(this._activeFilePath, this._logDirectory, this._baseName, archiveDay);
        EnforceRetention(this._logDirectory, this.MaxDays, this.MaxTotalBytes, now);
        this._writer = OpenAppendWriter(this._activeFilePath, out this._currentBytes);
    }
}
