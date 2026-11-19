// <copyright file="InMemoryHnmoPlayerStateRowBackend.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence;

using System.Collections.Concurrent;
using System.Text.Json;

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
