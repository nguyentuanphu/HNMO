// <copyright file="MountChainStore.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.MountChain;

using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.GiftCodes;

/// <summary>
/// Thread-safe persistent store over <see cref="HnmoStateStore"/> for the Mount Chain plug-in.
/// </summary>
public sealed class MountChainStore
{
    private const string SectionKey = "mountChain";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly HnmoStateStore _store;

    private MountChainStore(HnmoStateStore store) => this._store = store;

    /// <summary>Opens store on the given path.</summary>
    /// <param name="filePath">Path or null for temp store.</param>
    /// <returns>Store instance.</returns>
    public static MountChainStore Open(string? filePath)
    {
        var store = (string.IsNullOrWhiteSpace(filePath) ? null : HnmoStateStore.Open(filePath))
            ?? new HnmoStateStore(Path.Combine(Path.GetTempPath(), $"hnmo-mounts-{Guid.NewGuid():N}.json"));
        return new MountChainStore(store);
    }

    /// <summary>Reads store data under lock.</summary>
    /// <typeparam name="T">Projection type.</typeparam>
    /// <param name="read">Selector.</param>
    /// <returns>Projected snapshot.</returns>
    public T Read<T>(Func<MountChainStoreData, T> read)
    {
        lock (this._store.SyncRoot)
        {
            var data = this._store.ReadSection<MountChainStoreData>(SectionKey, JsonOptions) ?? new MountChainStoreData();
            return read(data);
        }
    }

    /// <summary>Mutates and persists store data atomically under lock.</summary>
    /// <param name="mutate">Mutator callback.</param>
    public void Update(Action<MountChainStoreData> mutate)
    {
        lock (this._store.SyncRoot)
        {
            var data = this._store.ReadSection<MountChainStoreData>(SectionKey, JsonOptions) ?? new MountChainStoreData();
            mutate(data);
            this._store.WriteSection(SectionKey, data, JsonOptions);
        }
    }
}
