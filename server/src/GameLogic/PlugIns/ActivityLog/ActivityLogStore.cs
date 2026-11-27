// <copyright file="ActivityLogStore.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
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

    internal const int BatchSize = 256;
    internal const int QueueCapacity = 4096;

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
    private readonly Dictionary<string, ActivityLogEntry> _entries = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<(ActivityLogEntry Entry, int RetentionDays)> _pending = new();
    private readonly List<(ActivityLogEntry Entry, int RetentionDays)> _retry = new(BatchSize);
    private IHnmoPlayerStateRowBackend? _loadedBackend;
    private int _pendingCount;
    private int _rejected;

    internal ActivityLogStore(string resolvedStatePath)
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
        var pending = (Normalize(entry), retentionDays);
        lock (this._gate)
        {
            this.WriteBatchLocked([pending]);
        }
    }

    /// <summary>Queues a gameplay event without calling the persistence backend.</summary>
    /// <param name="entry">The activity snapshot.</param>
    /// <param name="retentionDays">Active retention in days at the time of the event.</param>
    /// <returns>Whether the bounded queue accepted the event.</returns>
    public bool Enqueue(ActivityLogEntry entry, int retentionDays)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (Interlocked.Increment(ref this._pendingCount) > QueueCapacity)
        {
            Interlocked.Decrement(ref this._pendingCount);
            Interlocked.Increment(ref this._rejected);
            return false;
        }

        this._pending.Enqueue((Normalize(entry), retentionDays));
        return true;
    }

    internal static void FlushAllPending(ILogger logger)
    {
        foreach (var instance in Stores.Values)
        {
            if (!instance.IsValueCreated)
            {
                continue;
            }

            var store = instance.Value;
            var rejected = Interlocked.Exchange(ref store._rejected, 0);
            if (rejected > 0)
            {
                logger.LogWarning("Activity log queue was full and discarded {Count} gameplay events.", rejected);
            }

            try
            {
                store.FlushPending();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Activity log batch could not be stored. Queued records will be retried.");
            }
        }
    }

    internal int FlushPending()
    {
        if (Volatile.Read(ref this._pendingCount) == 0 || !Monitor.TryEnter(this._gate))
        {
            return 0;
        }

        try
        {
            var written = 0;
            while (written < QueueCapacity)
            {
                if (this._retry.Count == 0)
                {
                    while (this._retry.Count < BatchSize && this._pending.TryDequeue(out var pending))
                    {
                        this._retry.Add(pending);
                    }
                }

                if (this._retry.Count == 0)
                {
                    break;
                }

                // Keep the same row IDs if the database fails or acknowledgement is lost.
                this.WriteBatchLocked(this._retry);
                written += this._retry.Count;
                Interlocked.Add(ref this._pendingCount, -this._retry.Count);
                this._retry.Clear();
            }

            return written;
        }
        finally
        {
            Monitor.Exit(this._gate);
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
            var pending = this._retry.Concat(this._pending).Select(entry => entry.Entry);
            IEnumerable<ActivityLogEntry> seq = pending.Concat(this._entries.Values).DistinctBy(entry => entry.Id);

            if (query.Kind is { } kind)
            {
                seq = seq.Where(e => e.Kind == kind);
            }

            if (query.PvpMode is { } mode)
            {
                seq = seq.Where(entry => entry.PvpMode == mode);
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

            if (query.ItemSerial is { } origin)
            {
                var serial = origin.ToString("N");
                seq = seq.Where(entry => HasSerial(entry, serial));
            }

            if (!string.IsNullOrEmpty(item))
            {
                seq = seq.Where(e =>
                    e.ItemSummary.Contains(item, StringComparison.OrdinalIgnoreCase) ||
                    e.ItemIds.Contains(item, StringComparison.OrdinalIgnoreCase) ||
                    e.ItemSerials.Contains(item, StringComparison.OrdinalIgnoreCase) ||
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

    /// <summary>Gets matching activity rows for one real serial within the current retention window.</summary>
    /// <param name="serial">The origin item serial.</param>
    /// <param name="fromUtc">The inclusive UTC retention cutoff.</param>
    /// <param name="limit">The maximum number of records, between 1 and 101.</param>
    /// <returns>The matching active rows, newest first.</returns>
    public IReadOnlyList<ActivityLogEntry> QueryItemHistory(Guid serial, DateTime fromUtc, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 101);
        if (serial == Guid.Empty)
        {
            return [];
        }

        var backend = HnmoPlayerStateRowStoreRegistry.GetBackend(this._resolvedStatePath);
        if (backend is not IHnmoPlayerStateRowQueryBackend indexed || indexed.LoadActivityItemPage(serial, fromUtc, limit) is not { } rows)
        {
            return this.Query(new ActivityLogQuery { FromUtc = fromUtc, ItemSerial = serial, MaxResults = limit });
        }

        var token = serial.ToString("N");
        return ParseRows(rows, fromUtc, entry => HasSerial(entry, token));
    }

    /// <summary>Reads recorded player defeats for one exact combat mode during retention.</summary>
    /// <param name="mode">The mode recorded by combat at the time of the defeat.</param>
    /// <param name="fromUtc">The inclusive UTC retention cutoff.</param>
    /// <param name="limit">The maximum number of records, between 1 and 101.</param>
    /// <returns>Matching defeats, newest first.</returns>
    public IReadOnlyList<ActivityLogEntry> QueryPvpKills(ActivityPvpMode mode, DateTime fromUtc, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 101);
        const ActivityKind kind = ActivityKind.PvpKill;
        var backend = HnmoPlayerStateRowStoreRegistry.GetBackend(this._resolvedStatePath);
        if (backend is not IHnmoPlayerStateRowQueryBackend indexed || indexed.LoadActivityCombatPage((int)kind, (int)mode, fromUtc, limit) is not { } rows)
        {
            return this.Query(new ActivityLogQuery { FromUtc = fromUtc, Kind = kind, PvpMode = mode, MaxResults = limit });
        }

        return ParseRows(rows, fromUtc, entry => entry.Kind == kind && entry.PvpMode == mode);
    }

    private static bool HasSerial(ActivityLogEntry entry, string serial)
        => entry.ItemSerials?.Split(',').Contains(serial, StringComparer.Ordinal) is true;

    private static IReadOnlyList<ActivityLogEntry> ParseRows(IEnumerable<HnmoFeatureRow> rows, DateTime fromUtc, Func<ActivityLogEntry, bool> include)
    {
        var result = new List<ActivityLogEntry>();
        foreach (var row in rows)
        {
            try
            {
                if (JsonSerializer.Deserialize<ActivityLogEntry>(row.PayloadJson, JsonOptions) is { } entry
                    && entry.TimestampUtc >= fromUtc && include(entry))
                {
                    result.Add(entry);
                }
            }
            catch (JsonException)
            {
                // A corrupt historical row does not prevent other exact matches.
            }
        }

        return result;
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

    private static ActivityLogEntry Normalize(ActivityLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var utc = entry.TimestampUtc == default
            ? DateTime.UtcNow
            : DateTime.SpecifyKind(entry.TimestampUtc, DateTimeKind.Utc);
        var id = string.IsNullOrWhiteSpace(entry.Id)
            ? $"{utc:yyyyMMddHHmmssfff}_{Interlocked.Increment(ref _sequence):D8}_{Guid.NewGuid():N}"[..40]
            : entry.Id;

        return new ActivityLogEntry
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
            ItemSerials = entry.ItemSerials ?? string.Empty,
            BeforeItems = entry.BeforeItems,
            AfterItems = entry.AfterItems,
            ServerId = entry.ServerId,
            MapNumber = entry.MapNumber,
            PvpMode = entry.PvpMode,
            PickupQuantity = entry.PickupQuantity,
            ZenDelta = entry.ZenDelta,
            Details = entry.Details ?? string.Empty,
        };
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
                    this._entries[parsed.Id] = parsed;
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

    private void WriteBatchLocked(IReadOnlyCollection<(ActivityLogEntry Entry, int RetentionDays)> batch)
    {
        var rows = batch.Select(pending =>
        {
            var (entry, retentionDays) = pending;
            var expires = entry.TimestampUtc.AddDays(Math.Clamp(retentionDays, 1, 3650));
            return new HnmoFeatureRow(FeatureKey, entry.Id, JsonSerializer.Serialize(entry, JsonOptions), entry.TimestampUtc, expires);
        }).ToArray();
        this.EnsureLoadedLocked().UpsertBatch(rows, []);
        foreach (var (entry, _) in batch)
        {
            this._entries[entry.Id] = entry;
        }
    }
}
