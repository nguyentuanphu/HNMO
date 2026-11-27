// <copyright file="MarketStore.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ItemMarket;

using System.IO;
using System.Text.Json;

/// <summary>
/// Thread-safe persistence wrapper over <see cref="HnmoStateStore"/> providing atomic escrow transitions.
/// </summary>
public sealed class MarketStore
{
    private const string SectionKey = "itemMarket";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly HnmoStateStore _store;

    private MarketStore(HnmoStateStore store)
    {
        this._store = store;
    }

    /// <summary>Opens the market store for a file path.</summary>
    /// <param name="filePath">State file path, or null/blank for isolated temp storage.</param>
    /// <returns>The store.</returns>
    public static MarketStore Open(string? filePath)
    {
        var store = (string.IsNullOrWhiteSpace(filePath) ? null : HnmoStateStore.Open(filePath))
            ?? new HnmoStateStore(Path.Combine(Path.GetTempPath(), $"hnmo-market-{Guid.NewGuid():N}.json"));
        return new MarketStore(store);
    }

    /// <summary>Projects data atomically under lock.</summary>
    /// <typeparam name="TResult">Return type.</typeparam>
    /// <param name="reader">Projection function.</param>
    /// <returns>Projected result.</returns>
    public TResult Read<TResult>(Func<MarketStoreData, TResult> reader)
    {
        lock (this._store.SyncRoot)
        {
            var data = this._store.ReadSection<MarketStoreData>(SectionKey, JsonOptions) ?? new MarketStoreData();
            return reader(data);
        }
    }

    /// <summary>Mutates data atomically under lock and flushes when modified.</summary>
    /// <typeparam name="TResult">Return type.</typeparam>
    /// <param name="mutator">Atomic mutator.</param>
    /// <returns>Outcome.</returns>
    public TResult Mutate<TResult>(Func<MarketStoreData, (bool Changed, TResult Result)> mutator)
    {
        lock (this._store.SyncRoot)
        {
            var data = this._store.ReadSection<MarketStoreData>(SectionKey, JsonOptions) ?? new MarketStoreData();
            var (changed, result) = mutator(data);
            if (changed)
            {
                this._store.WriteSection(SectionKey, data, JsonOptions);
            }

            return result;
        }
    }
}
