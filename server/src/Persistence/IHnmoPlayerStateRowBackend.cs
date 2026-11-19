// <copyright file="IHnmoPlayerStateRowBackend.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence;

using System.Collections.Concurrent;
using System.Text.Json;

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
