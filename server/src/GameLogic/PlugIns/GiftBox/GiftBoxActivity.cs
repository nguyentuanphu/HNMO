// <copyright file="GiftBoxActivity.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GiftBox;

using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using HNMO.Persistence;

/// <summary>Keeps observed account activity in existing player-state rows for reviewed recent-player audiences.</summary>
public sealed class GiftBoxActivity
{
    /// <summary>The per-account observation feature; no separate save file is created.</summary>
    public const string Feature = "giftBox.activity";

    private static readonly ConditionalWeakTable<IHnmoPlayerStateRowBackend, Cache> Caches = new();
    private readonly IHnmoPlayerStateRowBackend _backend;
    private readonly Cache _cache;

    /// <summary>Creates a view over the existing shared player-state backend.</summary>
    /// <param name="backend">The host's PostgreSQL backend, or an isolated test backend.</param>
    public GiftBoxActivity(IHnmoPlayerStateRowBackend backend)
    {
        this._backend = backend;
        this._cache = Caches.GetValue(backend, _ => new());
    }

    /// <summary>Records an actually observed world entry or departure, never a guessed historical date.</summary>
    /// <param name="accountId">The authenticated account identifier from the player session.</param>
    /// <param name="atUtc">The authoritative UTC observation instant.</param>
    public void Observe(Guid accountId, DateTime atUtc)
    {
        if (accountId == Guid.Empty || atUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The gift box activity observation is invalid.");
        }

        lock (this._cache.Gate)
        {
            if (!this._cache.Loaded)
            {
                this.Load();
            }

            if (this._cache.Values.TryGetValue(accountId, out var previous) && previous >= atUtc)
            {
                return;
            }

            try
            {
                this._backend.UpsertBatch([new(Feature, accountId.ToString("N"), JsonSerializer.Serialize(atUtc), atUtc)], []);
                this._cache.Values[accountId] = atUtc;
            }
            catch
            {
                // A committed response may have been lost. Reload before another comparison/write.
                this._cache.Loaded = false;
                throw;
            }
        }
    }

    /// <summary>Reads a fresh detached observation snapshot for a host preview.</summary>
    /// <returns>Only observed account timestamps; absent accounts remain unknown.</returns>
    public IReadOnlyDictionary<Guid, DateTime> Read()
    {
        lock (this._cache.Gate)
        {
            this.Load();
            return new Dictionary<Guid, DateTime>(this._cache.Values);
        }
    }

    private void Load()
    {
        var entries = new Dictionary<Guid, DateTime>();
        foreach (var row in this._backend.LoadAll(Feature))
        {
            if (!Guid.TryParseExact(row.ScopeId, "N", out var accountId) || accountId == Guid.Empty)
            {
                throw new InvalidDataException("A gift box activity account identifier is invalid.");
            }

            DateTime observed;
            try
            {
                observed = JsonSerializer.Deserialize<DateTime>(row.PayloadJson);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("A gift box activity observation is unreadable.", exception);
            }

            if (observed.Kind != DateTimeKind.Utc)
            {
                throw new InvalidDataException("A gift box activity observation is not UTC.");
            }

            entries.Add(accountId, observed);
        }

        this._cache.Values = entries;
        this._cache.Loaded = true;
    }

    private sealed class Cache
    {
        public object Gate { get; } = new();

        public Dictionary<Guid, DateTime> Values { get; set; } = new();

        public bool Loaded { get; set; }
    }
}
