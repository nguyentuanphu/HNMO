// <copyright file="ActivityLogStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ActivityLog;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading;
using HNMO.Persistence;

/// <summary>
/// Persists and queries <see cref="ActivityLogEntry"/> records per row in <c>hnmo.player_feature_state</c>
/// (<see cref="IHnmoPlayerStateRowBackend"/>) using <c>activityLog.entry</c> feature keys.
/// </summary>
public sealed class ActivityLogStore
{
    /// <summary>Feature key stored in <c>hnmo.player_feature_state</c>.</summary>
    public const string FeatureKey = "activityLog.entry";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private static readonly ConcurrentDictionary<string, Lazy<ActivityLogStore>> Stores = new(PathComparer);
    private static long _sequence;

    private readonly object _gate = new();
    private readonly string _resolvedStatePath;
    private readonly List<ActivityLogEntry> _entries = [];
    private IHnmoPlayerStateRowBackend? _loadedBackend;

    private ActivityLogStore(string resolvedStatePath)
    {
        this._resolvedStatePath = resolvedStatePath;
    }

    /// <summary>
    /// Opens or returns the shared <see cref="ActivityLogStore"/> bound to the same database/store path as <paramref name="fileName"/>.
    /// </summary>
    /// <param name="fileName">State file key or path (defaults to <see cref="HnmoStateStore.StateFileName"/>).</param>
    /// <returns>The store instance.</returns>
    public static ActivityLogStore Open(string? fileName = null)
    {
        var key = string.IsNullOrWhiteSpace(fileName) ? HnmoStateStore.StateFileName : fileName.Trim();
        var resolved = HnmoStateStore.ResolveStateFilePath(key) ?? Path.GetFullPath(HnmoStateStore.StateFileName);
        return Stores.GetOrAdd(resolved, p => new Lazy<ActivityLogStore>(() => new ActivityLogStore(p), true)).Value;
    }

    /// <summary>Clears cached store wrappers (tests only).</summary>
    public static void ResetForTests() => Stores.Clear();

    /// <summary>
    /// Appends an entry and persists it immediately as an individual row in <see cref="IHnmoPlayerStateRowBackend"/>.
    /// </summary>
    /// <param name="entry">The activity entry.</param>
    /// <param name="retentionDays">Active retention window in days before automated archival.</param>
    public void Append(ActivityLogEntry entry, int retentionDays = 30)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var utc = entry.TimestampUtc == default
            ? DateTime.UtcNow
            : DateTime.SpecifyKind(entry.TimestampUtc, DateTimeKind.Utc);
        var days = Math.Clamp(retentionDays, 1, 3650);
        var id = string.IsNullOrWhiteSpace(entry.Id)
            ? $"{utc:yyyyMMddHHmmssfff}_{Interlocked.Increment(ref _sequence):D8}_{Guid.NewGuid():N}"[..40]
            : entry.Id;

        var normalized = new ActivityLogEntry
        {
            Id = id,
            TimestampUtc = utc,
            Kind = entry.Kind,
            Account = entry.Account ?? string.Empty,
            Character = entry.Character ?? string.Empty,
            PartnerAccount = entry.PartnerAccount ?? string.Empty,
            PartnerCharacter = entry.PartnerCharacter ?? string.Empty,
            ItemSummary = entry.ItemSummary ?? string.Empty,
            ItemIds = entry.ItemIds ?? string.Empty,
            ZenDelta = entry.ZenDelta,
            Details = entry.Details ?? string.Empty,
        };

        var json = JsonSerializer.Serialize(normalized, JsonOptions);
        var row = new HnmoFeatureRow(FeatureKey, id, json, utc, utc.AddDays(days));

        lock (this._gate)
        {
            var backend = this.EnsureLoadedLocked();
            backend.UpsertBatch([row], []);
            this._entries.Add(normalized);
        }
    }

    /// <summary>
    /// Queries activity entries matching <paramref name="query"/>, ordered newest-first.
    /// </summary>
    /// <param name="query">Optional filter criteria.</param>
    /// <returns>Matching entries.</returns>
    public IReadOnlyList<ActivityLogEntry> Query(ActivityLogQuery? query = null)
    {
        query ??= new ActivityLogQuery();
        var max = Math.Clamp(query.MaxResults <= 0 ? 200 : query.MaxResults, 1, 2000);
        var account = query.Account?.Trim();
        var character = query.Character?.Trim();
        var item = query.Item?.Trim();

        lock (this._gate)
        {
            this.EnsureLoadedLocked();
            IEnumerable<ActivityLogEntry> seq = this._entries;

            if (query.Kind is { } kind)
            {
                seq = seq.Where(e => e.Kind == kind);
            }

            if (query.FromUtc is { } fromUtc)
            {
                var from = DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc);
                seq = seq.Where(e => e.TimestampUtc >= from);
            }

            if (query.ToUtc is { } toUtc)
            {
                var to = DateTime.SpecifyKind(toUtc, DateTimeKind.Utc);
                seq = seq.Where(e => e.TimestampUtc <= to);
            }

            if (!string.IsNullOrEmpty(account))
            {
                seq = seq.Where(e =>
                    e.Account.Contains(account, StringComparison.OrdinalIgnoreCase) ||
                    e.PartnerAccount.Contains(account, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(character))
            {
                seq = seq.Where(e =>
                    e.Character.Contains(character, StringComparison.OrdinalIgnoreCase) ||
                    e.PartnerCharacter.Contains(character, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(item))
            {
                seq = seq.Where(e =>
                    e.ItemSummary.Contains(item, StringComparison.OrdinalIgnoreCase) ||
                    e.ItemIds.Contains(item, StringComparison.OrdinalIgnoreCase) ||
                    e.Details.Contains(item, StringComparison.OrdinalIgnoreCase));
            }

            return seq
                .OrderByDescending(e => e.TimestampUtc)
                .ThenByDescending(e => e.Id, StringComparer.Ordinal)
                .Take(max)
                .ToList();
        }
    }

    /// <summary>Reads the newest active rows using a bounded feature page on the production backend.</summary>
    /// <param name="fromUtc">The beginning of the current retention window.</param>
    /// <param name="limit">The maximum number of rows, between 1 and 101.</param>
    /// <returns>The recent activity entries.</returns>
    public IReadOnlyList<ActivityLogEntry> QueryRecent(DateTime fromUtc, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 101);
        var backend = HnmoPlayerStateRowStoreRegistry.GetBackend(this._resolvedStatePath);
        if (backend is not IHnmoPlayerStateRowQueryBackend indexed)
        {
            return this.Query(new ActivityLogQuery { FromUtc = fromUtc, MaxResults = limit });
        }

        var entries = new List<ActivityLogEntry>(limit);
        foreach (var row in indexed.LoadScopePage(FeatureKey, string.Empty, 0, limit))
        {
            if (row.UpdatedAtUtc < fromUtc)
            {
                break;
            }

            try
            {
                if (JsonSerializer.Deserialize<ActivityLogEntry>(row.PayloadJson, JsonOptions) is { } entry)
                {
                    entries.Add(entry);
                }
            }
            catch (JsonException)
            {
                // A corrupt row does not prevent the remaining results from being read.
            }
        }

        return entries;
    }

    /// <summary>
    /// Archives expired activity rows older than <paramref name="cutoffUtc"/> and reloads the cache.
    /// </summary>
    /// <param name="cutoffUtc">Archival cutoff timestamp (UTC).</param>
    /// <returns>Number of archived rows.</returns>
    public int ArchiveExpired(DateTime cutoffUtc)
    {
        lock (this._gate)
        {
            var backend = HnmoPlayerStateRowStoreRegistry.GetBackend(this._resolvedStatePath);
            var archived = backend.ArchiveExpired(cutoffUtc, FeatureKey);
            if (archived > 0)
            {
                this._loadedBackend = null;
                this.EnsureLoadedLocked();
            }

            return archived;
        }
    }

    private IHnmoPlayerStateRowBackend EnsureLoadedLocked()
    {
        var backend = HnmoPlayerStateRowStoreRegistry.GetBackend(this._resolvedStatePath);
        if (ReferenceEquals(this._loadedBackend, backend))
        {
            return backend;
        }

        this._entries.Clear();
        backend.EnsureSchema();
        foreach (var row in backend.LoadAll(FeatureKey))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<ActivityLogEntry>(row.PayloadJson, JsonOptions);
                if (parsed is not null)
                {
                    this._entries.Add(parsed);
                }
            }
            catch (JsonException)
            {
                // Ignore corrupt rows gracefully.
            }
        }

        this._loadedBackend = backend;
        return backend;
    }
}
