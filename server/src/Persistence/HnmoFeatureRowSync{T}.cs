// <copyright file="HnmoFeatureRowSync{T}.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence;

using System.Collections.Concurrent;
using System.Text.Json;

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
