// <copyright file="MarriageStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Marriage;

using System.IO;
using System.Text.Json;

/// <summary>
/// Thread-safe persistence wrapper over <see cref="HnmoStateStore"/> for Marriage state.
/// </summary>
public sealed class MarriageStore
{
    private const string SectionKey = "marriage";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly HnmoStateStore _store;

    private MarriageStore(HnmoStateStore store)
    {
        this._store = store;
    }

    /// <summary>Opens the marriage store for a file path.</summary>
    /// <param name="filePath">State file path, or empty/null for isolated temp store.</param>
    /// <returns>The store.</returns>
    public static MarriageStore Open(string? filePath)
    {
        var store = (string.IsNullOrWhiteSpace(filePath) ? null : HnmoStateStore.Open(filePath))
            ?? new HnmoStateStore(Path.Combine(Path.GetTempPath(), $"hnmo-marriage-{Guid.NewGuid():N}.json"));
        return new MarriageStore(store);
    }

    /// <summary>Projects data atomically under lock.</summary>
    /// <typeparam name="TResult">Return type.</typeparam>
    /// <param name="reader">Projection function.</param>
    /// <returns>Projected result.</returns>
    public TResult Read<TResult>(Func<MarriageStoreData, TResult> reader)
    {
        lock (this._store.SyncRoot)
        {
            var data = this._store.ReadSection<MarriageStoreData>(SectionKey, JsonOptions) ?? new MarriageStoreData();
            return reader(data);
        }
    }

    /// <summary>Mutates data atomically under lock and flushes when changed.</summary>
    /// <typeparam name="TResult">Return type.</typeparam>
    /// <param name="mutator">Mutator returning whether modified and the result.</param>
    /// <returns>Result.</returns>
    public TResult Mutate<TResult>(Func<MarriageStoreData, (bool Changed, TResult Result)> mutator)
    {
        lock (this._store.SyncRoot)
        {
            var data = this._store.ReadSection<MarriageStoreData>(SectionKey, JsonOptions) ?? new MarriageStoreData();
            var (changed, result) = mutator(data);
            if (changed)
            {
                this._store.WriteSection(SectionKey, data, JsonOptions);
            }

            return result;
        }
    }
}
