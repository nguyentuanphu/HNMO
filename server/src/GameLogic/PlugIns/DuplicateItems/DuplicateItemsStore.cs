// <copyright file="DuplicateItemsStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.DuplicateItems;

using System.IO;
using System.Text.Json;

/// <summary>
/// The duplicate cases in the HNMO state file, read and changed under the file's lock.
/// </summary>
public sealed class DuplicateItemsStore
{
    private const string SectionKey = "duplicateItems";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly HnmoStateStore _store;

    private DuplicateItemsStore(HnmoStateStore store)
    {
        this._store = store;
    }

    /// <summary>Opens the duplicate items store for a file path.</summary>
    /// <param name="filePath">State file path, or null/blank for isolated temp storage.</param>
    /// <returns>The store.</returns>
    public static DuplicateItemsStore Open(string? filePath)
    {
        var store = (string.IsNullOrWhiteSpace(filePath) ? null : HnmoStateStore.Open(filePath))
            ?? new HnmoStateStore(Path.Combine(Path.GetTempPath(), $"hnmo-duplicates-{Guid.NewGuid():N}.json"));
        return new DuplicateItemsStore(store);
    }

    /// <summary>Projects data atomically under lock.</summary>
    /// <typeparam name="TResult">Return type.</typeparam>
    /// <param name="reader">Projection function.</param>
    /// <returns>Projected result.</returns>
    public TResult Read<TResult>(Func<DuplicateItemsData, TResult> reader)
    {
        lock (this._store.SyncRoot)
        {
            var data = this._store.ReadSection<DuplicateItemsData>(SectionKey, JsonOptions) ?? new DuplicateItemsData();
            return reader(data);
        }
    }

    /// <summary>Mutates data atomically under lock and flushes when modified.</summary>
    /// <typeparam name="TResult">Return type.</typeparam>
    /// <param name="mutator">Atomic mutator.</param>
    /// <returns>Outcome.</returns>
    public TResult Mutate<TResult>(Func<DuplicateItemsData, (bool Changed, TResult Result)> mutator)
    {
        lock (this._store.SyncRoot)
        {
            var data = this._store.ReadSection<DuplicateItemsData>(SectionKey, JsonOptions) ?? new DuplicateItemsData();
            var (changed, result) = mutator(data);
            if (changed)
            {
                this._store.WriteSection(SectionKey, data, JsonOptions);
            }

            return result;
        }
    }
}
