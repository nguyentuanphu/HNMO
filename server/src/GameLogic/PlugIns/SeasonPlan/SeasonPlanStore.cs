// <copyright file="SeasonPlanStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.SeasonPlan;

using System.IO;
using System.Text.Json;

/// <summary>
/// Thread-safe persistence wrapper over <see cref="HnmoStateStore"/> for Season Plan progress.
/// </summary>
public sealed class SeasonPlanStore
{
    private const string SectionKey = "seasonPlan";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly HnmoStateStore _store;

    private SeasonPlanStore(HnmoStateStore store)
    {
        this._store = store;
    }

    /// <summary>Opens the Season Plan store for a file path.</summary>
    /// <param name="filePath">State file path, or null for temp storage.</param>
    /// <returns>Store instance.</returns>
    public static SeasonPlanStore Open(string? filePath)
    {
        var store = (string.IsNullOrWhiteSpace(filePath) ? null : HnmoStateStore.Open(filePath))
            ?? new HnmoStateStore(Path.Combine(Path.GetTempPath(), $"hnmo-season-{Guid.NewGuid():N}.json"));
        return new SeasonPlanStore(store);
    }

    /// <summary>Projects data atomically under lock.</summary>
    /// <typeparam name="TResult">Return type.</typeparam>
    /// <param name="reader">Reader delegate.</param>
    /// <returns>Projected result.</returns>
    public TResult Read<TResult>(Func<SeasonPlanStoreData, TResult> reader)
    {
        lock (this._store.SyncRoot)
        {
            var data = this._store.ReadSection<SeasonPlanStoreData>(SectionKey, JsonOptions) ?? new SeasonPlanStoreData();
            return reader(data);
        }
    }

    /// <summary>Mutates data atomically under lock and flushes when modified.</summary>
    /// <typeparam name="TResult">Return type.</typeparam>
    /// <param name="mutator">Mutator delegate.</param>
    /// <returns>Result.</returns>
    public TResult Mutate<TResult>(Func<SeasonPlanStoreData, (bool Changed, TResult Result)> mutator)
    {
        lock (this._store.SyncRoot)
        {
            var data = this._store.ReadSection<SeasonPlanStoreData>(SectionKey, JsonOptions) ?? new SeasonPlanStoreData();
            var (changed, result) = mutator(data);
            if (changed)
            {
                this._store.WriteSection(SectionKey, data, JsonOptions);
            }

            return result;
        }
    }
}
