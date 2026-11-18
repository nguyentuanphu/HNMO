// <copyright file="MountChainStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.MountChain;

using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.GiftCodes;

/// <summary>Per-account mount chain progress stored inside <c>mountChain</c>.</summary>
public sealed class MountAccountState
{
    /// <summary>Gets or sets Mount Shards balance earned from Golden Bosses and duplicate salvage.</summary>
    public int Shards { get; set; }

    /// <summary>Gets or sets unlocked mount tier IDs and unlock UTC timestamps.</summary>
    public Dictionary<string, DateTime> Unlocked { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets currently selected collection aura/tier ID for modest PvE bonus.</summary>
    public string ActiveTierId { get; set; } = string.Empty;
}

/// <summary>Persistent state stored under section <c>mountChain</c> in <see cref="HnmoStateStore"/>.</summary>
public sealed class MountChainStoreData
{
    /// <summary>Gets or sets per-account states keyed by login name.</summary>
    public Dictionary<string, MountAccountState> Accounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets owed mount items keyed by character ID when bag was full.</summary>
    public Dictionary<string, List<GiftCodeOwedReward>> Owed { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Thread-safe persistent store over <see cref="HnmoStateStore"/> for the Mount Chain plug-in.
/// </summary>
public sealed class MountChainStore
{
    private const string SectionKey = "mountChain";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly object _gate = new();
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
        lock (this._gate)
        {
            var data = this._store.ReadSection<MountChainStoreData>(SectionKey, JsonOptions) ?? new MountChainStoreData();
            return read(data);
        }
    }

    /// <summary>Mutates and persists store data atomically under lock.</summary>
    /// <param name="mutate">Mutator callback.</param>
    public void Update(Action<MountChainStoreData> mutate)
    {
        lock (this._gate)
        {
            var data = this._store.ReadSection<MountChainStoreData>(SectionKey, JsonOptions) ?? new MountChainStoreData();
            mutate(data);
            this._store.WriteSection(SectionKey, data, JsonOptions);
        }
    }
}
