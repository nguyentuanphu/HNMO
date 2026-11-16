// <copyright file="MaintenanceSettings.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Interfaces;

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Log rotation, backup retention, database cleanup, and disk alert settings saved alongside host settings in
/// <c>save/host-settings.json</c>.
/// </summary>
public sealed class MaintenanceSettings
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>Gets or sets maximum age (days) of rolled <c>.log.gz</c> files before deletion.</summary>
    public int MaxLogDays { get; set; } = 14;

    /// <summary>Gets or sets maximum total size (MB) of <c>save/logs</c>.</summary>
    public int MaxLogTotalMb { get; set; } = 200;

    /// <summary>Gets or sets maximum size (MB) of one active log file before rolling to <c>.log.gz</c>.</summary>
    public int MaxLogFileMb { get; set; } = 10;

    /// <summary>Gets or sets retention period (days) for database chat logs and admin audit entries (exported before delete).</summary>
    public int DbLogRetentionDays { get; set; } = 90;

    /// <summary>Gets or sets a value indicating whether daily automated backups run in the background.</summary>
    public bool AutoBackupEnabled { get; set; } = true;

    /// <summary>Gets or sets Sydney local hour (0-23) when daily automated backups run.</summary>
    public int AutoBackupHourSydney { get; set; } = 4;

    /// <summary>Gets or sets how many daily backups are retained.</summary>
    public int BackupKeepDaily { get; set; } = 7;

    /// <summary>Gets or sets how many weekly backups are retained.</summary>
    public int BackupKeepWeekly { get; set; } = 4;

    /// <summary>Gets or sets how many monthly backups are retained.</summary>
    public int BackupKeepMonthly { get; set; } = 3;

    /// <summary>Gets or sets interval (hours) between automatic PostgreSQL <c>VACUUM ANALYZE</c> runs.</summary>
    public int VacuumIntervalHours { get; set; } = 24;

    /// <summary>Gets or sets free disk threshold (MB) below which <c>/control</c> shows a low-disk warning.</summary>
    public int LowDiskWarningMb { get; set; } = 2048;

    /// <summary>Loads maintenance settings from <c>&lt;saveDir&gt;/host-settings.json</c> (preserving any existing keys).</summary>
    /// <param name="saveDirectory">Save folder path.</param>
    /// <returns>Normalized settings.</returns>
    public static MaintenanceSettings Load(string saveDirectory)
    {
        var path = Path.Combine(saveDirectory, "host-settings.json");
        var result = new MaintenanceSettings();
        if (!File.Exists(path))
        {
            return result;
        }

        try
        {
            var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
            var section = root?["Maintenance"] ?? root?["maintenance"] ?? root;
            if (section is JsonObject obj)
            {
                result.MaxLogDays = ReadInt(obj, nameof(MaxLogDays), result.MaxLogDays, 1, 3650);
                result.MaxLogTotalMb = ReadInt(obj, nameof(MaxLogTotalMb), result.MaxLogTotalMb, 10, 100_000);
                result.MaxLogFileMb = ReadInt(obj, nameof(MaxLogFileMb), result.MaxLogFileMb, 1, 1024);
                result.DbLogRetentionDays = ReadInt(obj, nameof(DbLogRetentionDays), result.DbLogRetentionDays, 1, 3650);
                result.AutoBackupEnabled = ReadBool(obj, nameof(AutoBackupEnabled), result.AutoBackupEnabled);
                result.AutoBackupHourSydney = ReadInt(obj, nameof(AutoBackupHourSydney), result.AutoBackupHourSydney, 0, 23);
                result.BackupKeepDaily = ReadInt(obj, nameof(BackupKeepDaily), result.BackupKeepDaily, 1, 365);
                result.BackupKeepWeekly = ReadInt(obj, nameof(BackupKeepWeekly), result.BackupKeepWeekly, 0, 104);
                result.BackupKeepMonthly = ReadInt(obj, nameof(BackupKeepMonthly), result.BackupKeepMonthly, 0, 120);
                result.VacuumIntervalHours = ReadInt(obj, nameof(VacuumIntervalHours), result.VacuumIntervalHours, 1, 720);
                result.LowDiskWarningMb = ReadInt(obj, nameof(LowDiskWarningMb), result.LowDiskWarningMb, 64, 1_000_000);
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // Keep safe defaults if file is mid-write or invalid JSON.
        }

        return result;
    }

    /// <summary>Atomically merges maintenance settings into <c>&lt;saveDir&gt;/host-settings.json</c> without dropping other host settings.</summary>
    /// <param name="saveDirectory">Save folder path.</param>
    /// <param name="settings">Settings to persist.</param>
    public static void Save(string saveDirectory, MaintenanceSettings settings)
    {
        Directory.CreateDirectory(saveDirectory);
        var path = Path.Combine(saveDirectory, "host-settings.json");
        JsonObject root;
        if (File.Exists(path))
        {
            try
            {
                root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? new JsonObject();
            }
            catch (JsonException)
            {
                root = new JsonObject();
            }
        }
        else
        {
            root = new JsonObject();
        }

        settings.Clamp();
        root["Maintenance"] = JsonSerializer.SerializeToNode(settings);
        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, root.ToJsonString(WriteOptions));
        File.Move(tempPath, path, overwrite: true);
    }

    /// <summary>Clamps numeric settings to safe positive ranges.</summary>
    public void Clamp()
    {
        this.MaxLogDays = Math.Clamp(this.MaxLogDays, 1, 3650);
        this.MaxLogTotalMb = Math.Clamp(this.MaxLogTotalMb, 10, 100_000);
        this.MaxLogFileMb = Math.Clamp(this.MaxLogFileMb, 1, 1024);
        this.DbLogRetentionDays = Math.Clamp(this.DbLogRetentionDays, 1, 3650);
        this.AutoBackupHourSydney = Math.Clamp(this.AutoBackupHourSydney, 0, 23);
        this.BackupKeepDaily = Math.Clamp(this.BackupKeepDaily, 1, 365);
        this.BackupKeepWeekly = Math.Clamp(this.BackupKeepWeekly, 0, 104);
        this.BackupKeepMonthly = Math.Clamp(this.BackupKeepMonthly, 0, 120);
        this.VacuumIntervalHours = Math.Clamp(this.VacuumIntervalHours, 1, 720);
        this.LowDiskWarningMb = Math.Clamp(this.LowDiskWarningMb, 64, 1_000_000);
    }

    private static int ReadInt(JsonObject obj, string propertyName, int fallback, int min, int max)
    {
        if (!TryGetCaseInsensitive(obj, propertyName, out var node) || node is null)
        {
            return fallback;
        }

        return node.AsValue().TryGetValue<int>(out var val) ? Math.Clamp(val, min, max) : fallback;
    }

    private static bool ReadBool(JsonObject obj, string propertyName, bool fallback)
    {
        if (!TryGetCaseInsensitive(obj, propertyName, out var node) || node is null)
        {
            return fallback;
        }

        return node.AsValue().TryGetValue<bool>(out var val) ? val : fallback;
    }

    private static bool TryGetCaseInsensitive(JsonObject obj, string propertyName, out JsonNode? value)
    {
        foreach (var kv in obj)
        {
            if (string.Equals(kv.Key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = kv.Value;
                return true;
            }
        }

        value = null;
        return false;
    }
}
