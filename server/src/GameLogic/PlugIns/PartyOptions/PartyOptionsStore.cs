// <copyright file="PartyOptionsStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.PartyOptions;

using System.IO;
using System.Text.Json;

/// <summary>
/// Thread-safe store over <see cref="HnmoStateStore"/> for Party Options public spot snapshots.
/// </summary>
public sealed class PartyOptionsStore
{
    private const string SectionKey = "partyOptions";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly HnmoStateStore _store;

    private PartyOptionsStore(HnmoStateStore store) => this._store = store;

    /// <summary>Opens store on the given path.</summary>
    /// <param name="filePath">Path or null for temp store.</param>
    /// <returns>Store instance.</returns>
    public static PartyOptionsStore Open(string? filePath)
    {
        var store = (string.IsNullOrWhiteSpace(filePath) ? null : HnmoStateStore.Open(filePath))
            ?? new HnmoStateStore(Path.Combine(Path.GetTempPath(), $"hnmo-party-{Guid.NewGuid():N}.json"));
        return new PartyOptionsStore(store);
    }

    /// <summary>Reads current public spot ads.</summary>
    /// <returns>Snapshot list.</returns>
    public List<PartyAdSnapshot> ReadAds()
    {
        lock (this._store.SyncRoot)
        {
            var data = this._store.ReadSection<PartyOptionsStoreData>(SectionKey, JsonOptions) ?? new PartyOptionsStoreData();
            return data.ActiveAds;
        }
    }

    /// <summary>Replaces saved public spot ads snapshot atomically.</summary>
    /// <param name="ads">Current active ads.</param>
    public void SaveAds(List<PartyAdSnapshot> ads)
    {
        lock (this._store.SyncRoot)
        {
            this._store.WriteSection(SectionKey, new PartyOptionsStoreData { ActiveAds = ads }, JsonOptions);
        }
    }
}
