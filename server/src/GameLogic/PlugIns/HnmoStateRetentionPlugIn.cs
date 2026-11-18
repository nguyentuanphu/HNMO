// <copyright file="HnmoStateRetentionPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.PeriodicTasks;
using HNMO.Persistence;
using HNMO.PlugIns;

/// <summary>
/// Configuration for <see cref="HnmoStateRetentionPlugIn"/> controlling archival of finished seasons/races and expired gift code claims.
/// </summary>
public sealed class HnmoStateRetentionConfiguration
{
    /// <summary>Gets or sets shared HNMO state key.</summary>
    [StringLength(260)]
    public string FileName { get; set; } = HnmoStateStore.StateFileName;

    /// <summary>Gets or sets number of days after completion/expiry before rows are moved to the archive table.</summary>
    [Range(1, 3650)]
    public int ArchiveFinishedDataAfterDays { get; set; } = 30;
}

/// <summary>Summary metadata stored in <see cref="HnmoStateStore"/> under section <c>stateRetention</c>.</summary>
public sealed class HnmoStateRetentionMetadata
{
    /// <summary>Gets or sets UTC timestamp of last archival sweep.</summary>
    public DateTime? LastArchivedAtUtc { get; set; }

    /// <summary>Gets or sets number of rows archived in the last sweep.</summary>
    public int LastArchivedCount { get; set; }

    /// <summary>Gets or sets total lifetime archived rows.</summary>
    public long TotalArchivedCount { get; set; }
}

/// <summary>
/// Scheduled daily archival plug-in that moves finished seasonal/race entries and expired gift-code claims older than
/// <see cref="HnmoStateRetentionConfiguration.ArchiveFinishedDataAfterDays"/> from <c>hnmo.player_feature_state</c>
/// into <c>hnmo.archived_feature_state</c>.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Player State Archival", Description = "Archives finished seasons/races and expired gift-code claims after N days.")]
[Guid("C4A7E219-8B35-4F1D-9E60-2A8D4C7F109B")]
public sealed class HnmoStateRetentionPlugIn :
    IPeriodicTaskPlugIn,
    ISupportCustomConfiguration<HnmoStateRetentionConfiguration>,
    ISupportDefaultCustomConfiguration
{
    private const string SectionKey = "stateRetention";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private DateTime _lastTickUtc = DateTime.MinValue;

    /// <inheritdoc />
    public HnmoStateRetentionConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new HnmoStateRetentionConfiguration();

    /// <inheritdoc />
    public void ForceStart() => this._lastTickUtc = DateTime.MinValue;

    /// <inheritdoc />
    public ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var now = DateTime.UtcNow;
        if (now - this._lastTickUtc < TimeSpan.FromHours(6))
        {
            return ValueTask.CompletedTask;
        }

        this._lastTickUtc = now;
        this.ArchiveNow(now);
        return ValueTask.CompletedTask;
    }

    /// <summary>Executes an immediate archival sweep using current configuration.</summary>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>Number of archived rows.</returns>
    public int ArchiveNow(DateTime utcNow)
    {
        var cfg = this.Configuration ?? new HnmoStateRetentionConfiguration();
        var days = Math.Max(1, cfg.ArchiveFinishedDataAfterDays);
        var cutoff = utcNow.AddDays(-days);
        var store = HnmoStateStore.Open(cfg.FileName);
        var backendCount = store is not null
            ? HnmoPlayerStateRowStoreRegistry.GetBackend(store.FilePath).ArchiveExpired(cutoff)
            : HnmoPlayerStateRowStoreRegistry.ArchiveAll(cutoff);

        if (store is not null)
        {
            var meta = store.ReadSection<HnmoStateRetentionMetadata>(SectionKey, JsonOptions) ?? new HnmoStateRetentionMetadata();
            meta.LastArchivedAtUtc = utcNow;
            meta.LastArchivedCount = backendCount;
            meta.TotalArchivedCount += backendCount;
            store.WriteSection(SectionKey, meta, JsonOptions);
        }

        return backendCount;
    }

    /// <summary>Reads retention metadata from <see cref="HnmoStateStore"/>.</summary>
    /// <param name="fileName">State file name.</param>
    /// <returns>Metadata snapshot.</returns>
    public static HnmoStateRetentionMetadata ReadMetadata(string? fileName)
    {
        var store = HnmoStateStore.Open(fileName);
        return store?.ReadSection<HnmoStateRetentionMetadata>(SectionKey, JsonOptions) ?? new HnmoStateRetentionMetadata();
    }
}
