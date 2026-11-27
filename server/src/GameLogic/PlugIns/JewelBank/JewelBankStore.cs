// <copyright file="JewelBankStore.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.JewelBank;

using System.IO;
using System.Text.Json;

/// <summary>
/// Thread-safe persistence wrapper over <see cref="HnmoStateStore"/> for Jewel Bank balances and withdrawals.
/// </summary>
public sealed class JewelBankStore
{
    private const string SectionKey = "jewelBank";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly HnmoStateStore _store;

    private JewelBankStore(HnmoStateStore store)
    {
        this._store = store;
    }

    /// <summary>Opens the Jewel Bank store for a file path.</summary>
    /// <param name="filePath">State file path, or null for isolated temp storage.</param>
    /// <returns>Store instance.</returns>
    public static JewelBankStore Open(string? filePath)
    {
        var store = (string.IsNullOrWhiteSpace(filePath) ? null : HnmoStateStore.Open(filePath))
            ?? new HnmoStateStore(Path.Combine(Path.GetTempPath(), $"hnmo-jewelbank-{Guid.NewGuid():N}.json"));
        return new JewelBankStore(store);
    }

    /// <summary>Projects data atomically under lock.</summary>
    /// <typeparam name="TResult">Return type.</typeparam>
    /// <param name="reader">Projection function.</param>
    /// <returns>Result.</returns>
    public TResult Read<TResult>(Func<JewelBankStoreData, TResult> reader)
    {
        lock (this._store.SyncRoot)
        {
            var data = this._store.ReadSection<JewelBankStoreData>(SectionKey, JsonOptions) ?? new JewelBankStoreData();
            return reader(data);
        }
    }

    /// <summary>Mutates data atomically under lock and flushes when changed.</summary>
    /// <typeparam name="TResult">Return type.</typeparam>
    /// <param name="mutator">Atomic mutator.</param>
    /// <returns>Result.</returns>
    public TResult Mutate<TResult>(Func<JewelBankStoreData, (bool Changed, TResult Result)> mutator)
    {
        lock (this._store.SyncRoot)
        {
            var data = this._store.ReadSection<JewelBankStoreData>(SectionKey, JsonOptions) ?? new JewelBankStoreData();
            var (changed, result) = mutator(data);
            if (changed)
            {
                this._store.WriteSection(SectionKey, data, JsonOptions);
            }

            return result;
        }
    }
}
