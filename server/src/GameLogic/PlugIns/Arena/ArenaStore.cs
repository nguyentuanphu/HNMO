// <copyright file="ArenaStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Arena;

using System.IO;
using System.Text.Json;

/// <summary>
/// The HNMO Arena's ranked records in the state file, read and changed under the file's lock.
/// </summary>
public sealed class ArenaStore
{
    private const string SectionKey = "arena";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly HnmoStateStore _store;

    private ArenaStore(HnmoStateStore store)
    {
        this._store = store;
    }

    /// <summary>Opens the Arena store for a file path.</summary>
    /// <param name="filePath">State file path, or null/blank for isolated temp storage.</param>
    /// <returns>The store.</returns>
    public static ArenaStore Open(string? filePath)
    {
        var store = (string.IsNullOrWhiteSpace(filePath) ? null : HnmoStateStore.Open(filePath))
            ?? new HnmoStateStore(Path.Combine(Path.GetTempPath(), $"hnmo-arena-{Guid.NewGuid():N}.json"));
        return new ArenaStore(store);
    }

    /// <summary>Projects data atomically under lock.</summary>
    /// <typeparam name="TResult">Return type.</typeparam>
    /// <param name="reader">Projection function.</param>
    /// <returns>Projected result.</returns>
    public TResult Read<TResult>(Func<ArenaStoreData, TResult> reader)
    {
        lock (this._store.SyncRoot)
        {
            var data = this._store.ReadSection<ArenaStoreData>(SectionKey, JsonOptions) ?? new ArenaStoreData();
            return reader(data);
        }
    }

    /// <summary>Mutates data atomically under lock and flushes when modified.</summary>
    /// <typeparam name="TResult">Return type.</typeparam>
    /// <param name="mutator">Atomic mutator.</param>
    /// <returns>Outcome.</returns>
    public TResult Mutate<TResult>(Func<ArenaStoreData, (bool Changed, TResult Result)> mutator)
    {
        lock (this._store.SyncRoot)
        {
            var data = this._store.ReadSection<ArenaStoreData>(SectionKey, JsonOptions) ?? new ArenaStoreData();
            var (changed, result) = mutator(data);
            if (changed)
            {
                this._store.WriteSection(SectionKey, data, JsonOptions);
            }

            return result;
        }
    }
}
