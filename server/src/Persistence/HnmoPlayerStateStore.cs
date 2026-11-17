// <copyright file="HnmoPlayerStateStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence;

using System.Collections.Concurrent;
using System.Text.Json;

/// <summary>
/// Single persisted row for per-account, per-character, or per-guild plug-in state.
/// </summary>
/// <param name="Feature">Logical feature key (e.g. <c>missions.account</c>, <c>accountSafety.account</c>).</param>
/// <param name="ScopeId">Account login, character ID, guild ID, or claim key.</param>
/// <param name="PayloadJson">Row JSON payload.</param>
/// <param name="UpdatedAtUtc">Last update timestamp (UTC).</param>
/// <param name="ExpiresAtUtc">Optional expiry/completion timestamp used for automated archival.</param>
public sealed record HnmoFeatureRow(
    string Feature,
    string ScopeId,
    string PayloadJson,
    DateTime UpdatedAtUtc,
    DateTime? ExpiresAtUtc = null);

/// <summary>
/// Storage backend abstraction for <see cref="HnmoFeatureRowSync{T}"/> (PostgreSQL in production, indexed in-memory in tests).
/// </summary>
public interface IHnmoPlayerStateRowBackend
{
    /// <summary>Creates tables/indexes if needed.</summary>
    void EnsureSchema();

    /// <summary>Loads all non-archived rows belonging to <paramref name="feature"/>.</summary>
    /// <param name="feature">Feature key.</param>
    /// <returns>Rows.</returns>
    IReadOnlyList<HnmoFeatureRow> LoadAll(string feature);

    /// <summary>Applies individual row upserts and deletes atomically.</summary>
    /// <param name="upserts">Rows to insert or update.</param>
    /// <param name="deletes">Primary keys to delete.</param>
    void UpsertBatch(IReadOnlyList<HnmoFeatureRow> upserts, IReadOnlyList<(string Feature, string ScopeId)> deletes);

    /// <summary>Moves expired/finished rows older than <paramref name="cutoffUtc"/> to the archive table.</summary>
    /// <param name="cutoffUtc">Cutoff timestamp (UTC).</param>
    /// <param name="feature">Optional feature filter.</param>
    /// <returns>Number of archived rows.</returns>
    int ArchiveExpired(DateTime cutoffUtc, string? feature = null);

    /// <summary>Counts active rows (optionally filtered by feature).</summary>
    /// <param name="feature">Optional feature filter.</param>
    /// <returns>Row count.</returns>
    int CountRows(string? feature = null);

    /// <summary>Counts archived rows (optionally filtered by feature).</summary>
    /// <param name="feature">Optional feature filter.</param>
    /// <returns>Archived row count.</returns>
    int CountArchivedRows(string? feature = null);
}

/// <summary>
/// Thread-safe indexed in-memory implementation of <see cref="IHnmoPlayerStateRowBackend"/> for tests and memory-only runs.
/// </summary>
public sealed class InMemoryHnmoPlayerStateRowBackend : IHnmoPlayerStateRowBackend
{
    private readonly object _gate = new();
    private readonly Dictionary<(string Feature, string ScopeId), HnmoFeatureRow> _rows = new();
    private readonly List<HnmoFeatureRow> _archived = [];

    /// <inheritdoc />
    public void EnsureSchema()
    {
    }

    /// <inheritdoc />
    public IReadOnlyList<HnmoFeatureRow> LoadAll(string feature)
    {
        lock (this._gate)
        {
            return this._rows.Values.Where(r => string.Equals(r.Feature, feature, StringComparison.Ordinal)).ToList();
        }
    }

    /// <inheritdoc />
    public void UpsertBatch(IReadOnlyList<HnmoFeatureRow> upserts, IReadOnlyList<(string Feature, string ScopeId)> deletes)
    {
        lock (this._gate)
        {
            foreach (var row in upserts)
            {
                this._rows[(row.Feature, row.ScopeId)] = row;
            }

            foreach (var key in deletes)
            {
                this._rows.Remove(key);
            }
        }
    }

    /// <inheritdoc />
    public int ArchiveExpired(DateTime cutoffUtc, string? feature = null)
    {
        lock (this._gate)
        {
            var expired = this._rows
                .Where(kv => (feature is null || string.Equals(kv.Key.Feature, feature, StringComparison.Ordinal))
                    && kv.Value.ExpiresAtUtc is { } exp && exp <= cutoffUtc)
                .ToList();
            foreach (var kv in expired)
            {
                this._rows.Remove(kv.Key);
                this._archived.Add(kv.Value);
            }

            return expired.Count;
        }
    }

    /// <inheritdoc />
    public int CountRows(string? feature = null)
    {
        lock (this._gate)
        {
            return feature is null
                ? this._rows.Count
                : this._rows.Count(kv => string.Equals(kv.Key.Feature, feature, StringComparison.Ordinal));
        }
    }

    /// <inheritdoc />
    public int CountArchivedRows(string? feature = null)
    {
        lock (this._gate)
        {
            return feature is null
                ? this._archived.Count
                : this._archived.Count(r => string.Equals(r.Feature, feature, StringComparison.Ordinal));
        }
    }
}

/// <summary>
/// Global registry linking per-state-file stores to PostgreSQL when available, or isolated in-memory backends in tests.
/// </summary>
public static class HnmoPlayerStateRowStoreRegistry
{
    private static readonly ConcurrentDictionary<string, IHnmoPlayerStateRowBackend> Backends = new(StringComparer.OrdinalIgnoreCase);
    private static Func<string, IHnmoPlayerStateRowBackend>? _postgresFactory;

    /// <summary>Configures the PostgreSQL backend factory for non-empty file keys.</summary>
    /// <param name="factory">Factory returning a PostgreSQL backend.</param>
    public static void ConfigurePostgresFactory(Func<string, IHnmoPlayerStateRowBackend> factory)
    {
        _postgresFactory = factory;
    }

    /// <summary>Clears any configured PostgreSQL factory (for isolated unit tests).</summary>
    public static void ResetForTests()
    {
        _postgresFactory = null;
        Backends.Clear();
    }

    /// <summary>Resolves or creates the <see cref="IHnmoPlayerStateRowBackend"/> for <paramref name="stateKey"/>.</summary>
    /// <param name="stateKey">State file path or empty string for in-memory isolation.</param>
    /// <returns>Backend instance.</returns>
    public static IHnmoPlayerStateRowBackend GetBackend(string? stateKey)
    {
        if (string.IsNullOrWhiteSpace(stateKey))
        {
            return new InMemoryHnmoPlayerStateRowBackend();
        }

        var normalized = stateKey.Trim();
        return Backends.GetOrAdd(normalized, k =>
        {
            var backend = _postgresFactory is not null ? _postgresFactory(k) : new InMemoryHnmoPlayerStateRowBackend();
            backend.EnsureSchema();
            return backend;
        });
    }

    /// <summary>Registers an explicit backend for <paramref name="stateKey"/> (used in integration tests).</summary>
    /// <param name="stateKey">Normalized file path key.</param>
    /// <param name="backend">Backend instance.</param>
    public static void RegisterBackend(string stateKey, IHnmoPlayerStateRowBackend backend)
    {
        backend.EnsureSchema();
        Backends[stateKey.Trim()] = backend;
    }

    /// <summary>Runs archival across all registered backends.</summary>
    /// <param name="cutoffUtc">Archive items expired on or before this UTC time.</param>
    /// <param name="feature">Optional feature filter.</param>
    /// <returns>Total archived row count.</returns>
    public static int ArchiveAll(DateTime cutoffUtc, string? feature = null) =>
        Backends.Values.Distinct().Sum(b => b.ArchiveExpired(cutoffUtc, feature));
}

/// <summary>
/// Diff-sync helper that persists ONLY changed per-account / per-character rows individually into <see cref="IHnmoPlayerStateRowBackend"/>
/// and performs automatic one-shot migration from legacy unified JSON dictionaries.
/// </summary>
/// <typeparam name="T">Row payload type.</typeparam>
public sealed class HnmoFeatureRowSync<T>
{
    private readonly Dictionary<string, string> _lastJson = new(StringComparer.Ordinal);
    private readonly HashSet<string> _dirtyKeys = new(StringComparer.Ordinal);
    private bool _fullScanPending;

    /// <summary>Marks a specific <paramref name="scopeId"/> dirty so <see cref="SaveChanges"/> skips unchanged entries.</summary>
    /// <param name="scopeId">Row key.</param>
    public void MarkDirty(string scopeId) => this._dirtyKeys.Add(scopeId);

    /// <summary>Marks that the next <see cref="SaveChanges"/> must diff the entire dictionary (when mutations did not track keys).</summary>
    public void MarkFullScanPending() => this._fullScanPending = true;

    /// <summary>
    /// Loads existing rows from <paramref name="backend"/>; if DB has none and <paramref name="legacyJsonDict"/> has rows,
    /// migrates them in one batch to DB and clears <paramref name="legacyJsonDict"/>.
    /// </summary>
    /// <param name="backend">Row backend.</param>
    /// <param name="feature">Feature key.</param>
    /// <param name="legacyJsonDict">In-memory/JSON dictionary.</param>
    /// <param name="options">Serializer options.</param>
    /// <param name="getExpiryUtc">Optional expiry selector.</param>
    /// <returns><c>true</c> if legacy entries were migrated and <paramref name="legacyJsonDict"/> cleared.</returns>
    public bool LoadOrMigrate(
        IHnmoPlayerStateRowBackend backend,
        string feature,
        Dictionary<string, T> legacyJsonDict,
        JsonSerializerOptions options,
        Func<string, T, DateTime?>? getExpiryUtc = null)
    {
        var dbRows = backend.LoadAll(feature);
        if (dbRows.Count > 0)
        {
            legacyJsonDict.Clear();
            this._lastJson.Clear();
            foreach (var row in dbRows)
            {
                try
                {
                    var val = JsonSerializer.Deserialize<T>(row.PayloadJson, options);
                    if (val is not null)
                    {
                        legacyJsonDict[row.ScopeId] = val;
                        this._lastJson[row.ScopeId] = row.PayloadJson;
                    }
                }
                catch (JsonException)
                {
                    // Skip corrupt row safely.
                }
            }

            return false;
        }

        if (legacyJsonDict.Count == 0)
        {
            return false;
        }

        // First start migration: move legacy JSON dictionary entries into individual database rows.
        var now = DateTime.UtcNow;
        var batch = new List<HnmoFeatureRow>(legacyJsonDict.Count);
        foreach (var (key, val) in legacyJsonDict)
        {
            var json = JsonSerializer.Serialize(val, options);
            this._lastJson[key] = json;
            batch.Add(new HnmoFeatureRow(feature, key, json, now, getExpiryUtc?.Invoke(key, val)));
        }

        backend.UpsertBatch(batch, []);
        return true;
    }

    /// <summary>
    /// Upserts ONLY rows that changed (either tracked via <see cref="MarkDirty"/> or detected on scan)
    /// and deletes removed keys, returning the number of database rows written.
    /// </summary>
    /// <param name="backend">Row backend.</param>
    /// <param name="feature">Feature key.</param>
    /// <param name="currentDict">Current live dictionary.</param>
    /// <param name="options">Serializer options.</param>
    /// <param name="getExpiryUtc">Optional expiry selector.</param>
    /// <param name="useDirtySetOnly">If true and dirty keys were recorded, only inspect dirty/new/deleted keys (O(dirty) instead of O(N)).</param>
    /// <returns>Number of upserted + deleted rows.</returns>
    public int SaveChanges(
        IHnmoPlayerStateRowBackend backend,
        string feature,
        Dictionary<string, T> currentDict,
        JsonSerializerOptions options,
        Func<string, T, DateTime?>? getExpiryUtc = null,
        bool useDirtySetOnly = false)
    {
        var now = DateTime.UtcNow;
        var upserts = new List<HnmoFeatureRow>();
        var deletes = new List<(string, string)>();

        if (useDirtySetOnly && !this._fullScanPending && this._dirtyKeys.Count > 0)
        {
            foreach (var key in this._dirtyKeys)
            {
                if (currentDict.TryGetValue(key, out var val))
                {
                    var json = JsonSerializer.Serialize(val, options);
                    if (!this._lastJson.TryGetValue(key, out var prev) || !string.Equals(prev, json, StringComparison.Ordinal))
                    {
                        this._lastJson[key] = json;
                        upserts.Add(new HnmoFeatureRow(feature, key, json, now, getExpiryUtc?.Invoke(key, val)));
                    }
                }
                else if (this._lastJson.Remove(key))
                {
                    deletes.Add((feature, key));
                }
            }

            this._dirtyKeys.Clear();
        }
        else
        {
            foreach (var (key, val) in currentDict)
            {
                var json = JsonSerializer.Serialize(val, options);
                if (!this._lastJson.TryGetValue(key, out var prev) || !string.Equals(prev, json, StringComparison.Ordinal))
                {
                    this._lastJson[key] = json;
                    upserts.Add(new HnmoFeatureRow(feature, key, json, now, getExpiryUtc?.Invoke(key, val)));
                }
            }

            foreach (var oldKey in this._lastJson.Keys.Where(k => !currentDict.ContainsKey(k)).ToList())
            {
                this._lastJson.Remove(oldKey);
                deletes.Add((feature, oldKey));
            }

            this._dirtyKeys.Clear();
            this._fullScanPending = false;
        }

        if (upserts.Count > 0 || deletes.Count > 0)
        {
            backend.UpsertBatch(upserts, deletes);
        }

        return upserts.Count + deletes.Count;
    }
}
