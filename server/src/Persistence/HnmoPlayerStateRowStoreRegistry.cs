// <copyright file="HnmoPlayerStateRowStoreRegistry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence;

using System.Collections.Concurrent;
using System.Text.Json;

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
