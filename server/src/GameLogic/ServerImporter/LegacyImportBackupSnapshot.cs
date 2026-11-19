// <copyright file="LegacyImportBackupSnapshot.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ServerImporter;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

/// <summary>
/// Persisted pre-import state snapshot enabling one-click undo after a local server migration.
/// Stored exclusively on the local host (never in source control).
/// </summary>
public sealed class LegacyImportBackupSnapshot
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Gets or sets the unique backup identifier.</summary>
    public string BackupId { get; set; } = string.Empty;

    /// <summary>Gets or sets UTC creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets Sydney display timestamp.</summary>
    public string CreatedAtSydney { get; set; } = string.Empty;

    /// <summary>Gets or sets the detected legacy server version.</summary>
    public LegacyServerVersion SourceVersion { get; set; }

    /// <summary>Gets or sets source folder path.</summary>
    public string SourceFolder { get; set; } = string.Empty;

    /// <summary>Gets or sets previous <c>ExperienceRate</c>.</summary>
    public float PreviousExperienceRate { get; set; }

    /// <summary>Gets or sets previous <c>MasterExperienceRate</c>.</summary>
    public float PreviousMasterExperienceRate { get; set; }

    /// <summary>Gets or sets previous <c>ShouldDropMoney</c> value.</summary>
    public bool PreviousShouldDropMoney { get; set; }

    /// <summary>Gets or sets a value indicating whether rate fields were modified.</summary>
    public bool ModifiedRates { get; set; }

    /// <summary>Gets or sets previous plugin configuration JSON blobs keyed by plugin TypeId string.</summary>
    public List<BackupPlugInState> PreviousPlugIns { get; set; } = [];

    /// <summary>Gets or sets previous merchant shop contents before overwrite.</summary>
    public List<BackupMerchantShop> PreviousMerchantShops { get; set; } = [];

    /// <summary>Gets or sets descriptions of <see cref="HNMO.DataModel.Configuration.DropItemGroup"/> entries created by this import.</summary>
    public List<string> CreatedDropGroupTags { get; set; } = [];

    /// <summary>Gets or sets descriptors of <see cref="HNMO.DataModel.Configuration.MonsterSpawnArea"/> entries created by this import.</summary>
    public List<BackupSpawnEntry> CreatedSpawnAreas { get; set; } = [];

    /// <summary>Gets or sets login names of newly created accounts.</summary>
    public List<string> CreatedAccountLogins { get; set; } = [];

    /// <summary>Gets or sets names of newly created guilds.</summary>
    public List<string> CreatedGuildNames { get; set; } = [];

    /// <summary>Gets or sets concise summary text.</summary>
    public string SummaryText { get; set; } = string.Empty;

    /// <summary>
    /// Writes a backup snapshot atomically using temp-file swap.
    /// </summary>
    /// <param name="backupDirectory">Target local directory.</param>
    /// <param name="snapshot">Snapshot to save.</param>
    /// <returns>The full file path.</returns>
    public static string SaveAtomic(string backupDirectory, LegacyImportBackupSnapshot snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupDirectory);
        ArgumentNullException.ThrowIfNull(snapshot);
        Directory.CreateDirectory(backupDirectory);

        var fileName = $"{snapshot.BackupId}.json";
        var target = Path.Combine(backupDirectory, fileName);
        var tmp = Path.Combine(backupDirectory, $"{fileName}.{Guid.NewGuid():N}.tmp");
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        File.WriteAllText(tmp, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(tmp, target, overwrite: true);
        return target;
    }

    /// <summary>
    /// Lists all saved backup snapshots in reverse chronological order.
    /// </summary>
    /// <param name="backupDirectory">The backup directory.</param>
    /// <returns>Loaded snapshots.</returns>
    public static List<LegacyImportBackupSnapshot> ListAll(string backupDirectory)
    {
        if (string.IsNullOrWhiteSpace(backupDirectory) || !Directory.Exists(backupDirectory))
        {
            return [];
        }

        var list = new List<LegacyImportBackupSnapshot>();
        foreach (var file in Directory.EnumerateFiles(backupDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var snap = JsonSerializer.Deserialize<LegacyImportBackupSnapshot>(File.ReadAllText(file), JsonOptions);
                if (snap is not null && !string.IsNullOrWhiteSpace(snap.BackupId))
                {
                    list.Add(snap);
                }
            }
            catch
            {
                // Skip unreadable backup files cleanly
            }
        }

        return list.OrderByDescending(s => s.CreatedAtUtc).ToList();
    }

    /// <summary>Loads a specific backup snapshot by ID.</summary>
    /// <param name="backupDirectory">The backup directory.</param>
    /// <param name="backupId">The backup identifier.</param>
    /// <returns>The snapshot or <see langword="null"/> if missing.</returns>
    public static LegacyImportBackupSnapshot? TryLoad(string backupDirectory, string backupId)
    {
        if (string.IsNullOrWhiteSpace(backupDirectory) || string.IsNullOrWhiteSpace(backupId))
        {
            return null;
        }

        var safeId = Path.GetFileName(backupId.Trim());
        var path = Path.Combine(backupDirectory, $"{safeId}.json");
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<LegacyImportBackupSnapshot>(File.ReadAllText(path), JsonOptions);
    }

    /// <summary>Deletes a backup file after undo if desired.</summary>
    /// <param name="backupDirectory">The backup directory.</param>
    /// <param name="backupId">The backup identifier.</param>
    public static void Delete(string backupDirectory, string backupId)
    {
        var safeId = Path.GetFileName(backupId.Trim());
        var path = Path.Combine(backupDirectory, $"{safeId}.json");
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

/// <summary>Saved state of a single <see cref="HNMO.PlugIns.PlugInConfiguration"/> before import.</summary>
public sealed class BackupPlugInState
{
    /// <summary>Gets or sets plugin GUID.</summary>
    public Guid TypeId { get; set; }

    /// <summary>Gets or sets whether a configuration record existed prior to import.</summary>
    public bool Existed { get; set; }

    /// <summary>Gets or sets previous IsActive flag.</summary>
    public bool PreviousIsActive { get; set; }

    /// <summary>Gets or sets previous JSON configuration payload.</summary>
    public string? PreviousJson { get; set; }
}

/// <summary>Saved merchant store snapshot before shop import.</summary>
public sealed class BackupMerchantShop
{
    /// <summary>Gets or sets merchant NPC monster number.</summary>
    public int NpcNumber { get; set; }

    /// <summary>Gets or sets whether a <c>MerchantStore</c> existed before import.</summary>
    public bool HadMerchantStore { get; set; }

    /// <summary>Gets or sets previous items.</summary>
    public List<BackupItemSlot> PreviousItems { get; set; } = [];
}

/// <summary>Saved minimal item snapshot inside a merchant shop backup.</summary>
public sealed class BackupItemSlot
{
    /// <summary>Gets or sets slot index.</summary>
    public byte ItemSlot { get; set; }

    /// <summary>Gets or sets item group.</summary>
    public byte Group { get; set; }

    /// <summary>Gets or sets item number.</summary>
    public short Number { get; set; }

    /// <summary>Gets or sets item level.</summary>
    public byte Level { get; set; }

    /// <summary>Gets or sets durability.</summary>
    public double Durability { get; set; }

    /// <summary>Gets or sets whether item has skill.</summary>
    public bool HasSkill { get; set; }
}

/// <summary>Identifies a created <see cref="HNMO.DataModel.Configuration.MonsterSpawnArea"/> for undo removal.</summary>
public sealed class BackupSpawnEntry
{
    /// <summary>Gets or sets map number.</summary>
    public short MapNumber { get; set; }

    /// <summary>Gets or sets monster number.</summary>
    public short MonsterNumber { get; set; }

    /// <summary>Gets or sets X1.</summary>
    public byte X1 { get; set; }

    /// <summary>Gets or sets Y1.</summary>
    public byte Y1 { get; set; }

    /// <summary>Gets or sets X2.</summary>
    public byte X2 { get; set; }

    /// <summary>Gets or sets Y2.</summary>
    public byte Y2 { get; set; }

    /// <summary>Gets or sets quantity.</summary>
    public short Quantity { get; set; }
}
