// <copyright file="PartyOptionsStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.PartyOptions;

using System.IO;
using System.Text.Json;

/// <summary>Saved party advertisement record for persistence across reconnects or website preview.</summary>
public sealed class PartyAdSnapshot
{
    /// <summary>Gets or sets leader character name.</summary>
    public string LeaderName { get; set; } = string.Empty;

    /// <summary>Gets or sets map name.</summary>
    public string MapName { get; set; } = string.Empty;

    /// <summary>Gets or sets hunting spot label.</summary>
    public string SpotName { get; set; } = string.Empty;

    /// <summary>Gets or sets min level.</summary>
    public int MinLevel { get; set; } = 1;

    /// <summary>Gets or sets max level (0 = no upper limit).</summary>
    public int MaxLevel { get; set; }

    /// <summary>Gets or sets a value indicating whether a password is required.</summary>
    public bool HasPassword { get; set; }

    /// <summary>Gets or sets current member count.</summary>
    public int MemberCount { get; set; } = 1;

    /// <summary>Gets or sets max party size.</summary>
    public int MaxSize { get; set; } = 5;

    /// <summary>Gets or sets UTC timestamp.</summary>
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>Persistent state stored under section <c>partyOptions</c> in <see cref="HnmoStateStore"/>.</summary>
public sealed class PartyOptionsStoreData
{
    /// <summary>Gets or sets latest public spot advertisements snapshot.</summary>
    public List<PartyAdSnapshot> ActiveAds { get; set; } = [];
}

/// <summary>
/// Thread-safe store over <see cref="HnmoStateStore"/> for Party Options public spot snapshots.
/// </summary>
public sealed class PartyOptionsStore
{
    private const string SectionKey = "partyOptions";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly object _gate = new();
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
        lock (this._gate)
        {
            var data = this._store.ReadSection<PartyOptionsStoreData>(SectionKey, JsonOptions) ?? new PartyOptionsStoreData();
            return data.ActiveAds;
        }
    }

    /// <summary>Replaces saved public spot ads snapshot atomically.</summary>
    /// <param name="ads">Current active ads.</param>
    public void SaveAds(List<PartyAdSnapshot> ads)
    {
        lock (this._gate)
        {
            this._store.WriteSection(SectionKey, new PartyOptionsStoreData { ActiveAds = ads }, JsonOptions);
        }
    }
}
