// <copyright file="LuckyNumberStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.LuckyNumber;

using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;

/// <summary>
/// Thread-safe persistence wrapper over <see cref="HnmoStateStore"/> for Daily Lucky Number rounds and rewards.
/// </summary>
public sealed class LuckyNumberStore
{
    private const string SectionKey = "luckyNumber";
    private const int MaxStoredRounds = 30;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly HnmoStateStore _store;

    private LuckyNumberStore(HnmoStateStore store)
    {
        this._store = store;
    }

    /// <summary>Opens the shared store for the specified file path.</summary>
    /// <param name="filePath">The file path, or empty/null for an isolated temp store.</param>
    /// <returns>The store.</returns>
    public static LuckyNumberStore Open(string? filePath)
    {
        var store = (string.IsNullOrWhiteSpace(filePath)
            ? null
            : HnmoStateStore.Open(filePath))
            ?? new HnmoStateStore(Path.Combine(Path.GetTempPath(), $"hnmo-lucky-{Guid.NewGuid():N}.json"));
        return new LuckyNumberStore(store);
    }

    /// <summary>Reads the current data under a lock and transforms it.</summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="reader">The read projection.</param>
    /// <returns>The projected value.</returns>
    public TResult Read<TResult>(Func<LuckyNumberStoreData, TResult> reader)
    {
        lock (this._store.SyncRoot)
        {
            var data = this._store.ReadSection<LuckyNumberStoreData>(SectionKey, JsonOptions) ?? new LuckyNumberStoreData();
            return reader(data);
        }
    }

    /// <summary>Mutates the data under a lock and saves to <see cref="HnmoStateStore"/>.</summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="mutator">The mutator returning result and whether changes occurred.</param>
    /// <returns>The result.</returns>
    public TResult Mutate<TResult>(Func<LuckyNumberStoreData, (bool Changed, TResult Result)> mutator)
    {
        lock (this._store.SyncRoot)
        {
            var data = this._store.ReadSection<LuckyNumberStoreData>(SectionKey, JsonOptions) ?? new LuckyNumberStoreData();
            var (changed, result) = mutator(data);
            if (changed)
            {
                if (data.Rounds.Count > MaxStoredRounds)
                {
                    data.Rounds = data.Rounds
                        .OrderByDescending(r => r.DateKey, StringComparer.Ordinal)
                        .Take(MaxStoredRounds)
                        .ToList();
                }

                this._store.WriteSection(SectionKey, data, JsonOptions);
            }

            return result;
        }
    }
}
