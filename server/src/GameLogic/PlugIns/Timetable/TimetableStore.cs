// <copyright file="TimetableStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Timetable;

using System.IO;
using System.Text.Json;

/// <summary>
/// Recorded winner of a Find the GM event run.
/// </summary>
public sealed class FindGmWinnerEntry
{
    /// <summary>Gets or sets the winning character name.</summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>Gets or sets the map where the GM was found.</summary>
    public string MapName { get; set; } = string.Empty;

    /// <summary>Gets or sets the coins awarded.</summary>
    public int Coins { get; set; }

    /// <summary>Gets or sets the coin type.</summary>
    public string CoinType { get; set; } = string.Empty;

    /// <summary>Gets or sets the zen awarded.</summary>
    public int Zen { get; set; }

    /// <summary>Gets or sets the UTC claim timestamp.</summary>
    public DateTime WonAtUtc { get; set; }
}

/// <summary>
/// Persisted section stored under <c>timetable</c> in <see cref="HnmoStateStore"/>.
/// </summary>
public sealed class TimetableStoreData
{
    /// <summary>Gets or sets recent Find the GM winners, newest first.</summary>
    public List<FindGmWinnerEntry> FindGmWinners { get; set; } = [];
}

/// <summary>
/// Thread-safe persistence wrapper over <see cref="HnmoStateStore"/> for timetable history.
/// </summary>
public sealed class TimetableStore
{
    private const string SectionKey = "timetable";
    private const int MaxStoredWinners = 20;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly object _gate = new();
    private readonly HnmoStateStore _store;

    private TimetableStore(HnmoStateStore store)
    {
        this._store = store;
    }

    /// <summary>Opens the timetable store for the configured file path (or in-memory when blank).</summary>
    /// <param name="filePath">The configured state file path.</param>
    /// <returns>The timetable store.</returns>
    public static TimetableStore Open(string? filePath)
    {
        var store = (string.IsNullOrWhiteSpace(filePath)
            ? null
            : HnmoStateStore.Open(filePath))
            ?? new HnmoStateStore(Path.Combine(Path.GetTempPath(), $"hnmo-timetable-{Guid.NewGuid():N}.json"));
        return new TimetableStore(store);
    }

    /// <summary>Records a Find the GM event winner and saves the state store.</summary>
    /// <param name="characterName">The winner's character name.</param>
    /// <param name="mapName">The map name.</param>
    /// <param name="coins">The coins awarded.</param>
    /// <param name="coinType">The coin type name.</param>
    /// <param name="zen">The zen awarded.</param>
    /// <param name="utcNow">The claim time in UTC.</param>
    public void RecordFindGmWinner(string characterName, string mapName, int coins, string coinType, int zen, DateTime utcNow)
    {
        lock (this._gate)
        {
            var data = this._store.ReadSection<TimetableStoreData>(SectionKey, JsonOptions) ?? new TimetableStoreData();
            data.FindGmWinners.Insert(0, new FindGmWinnerEntry
            {
                CharacterName = characterName,
                MapName = mapName,
                Coins = coins,
                CoinType = coinType,
                Zen = zen,
                WonAtUtc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc),
            });

            if (data.FindGmWinners.Count > MaxStoredWinners)
            {
                data.FindGmWinners.RemoveRange(MaxStoredWinners, data.FindGmWinners.Count - MaxStoredWinners);
            }

            this._store.WriteSection(SectionKey, data, JsonOptions);
        }
    }

    /// <summary>Reads a snapshot of recent Find the GM winners.</summary>
    /// <returns>The list of recent winners.</returns>
    public IReadOnlyList<FindGmWinnerEntry> ReadFindGmWinners()
    {
        lock (this._gate)
        {
            var data = this._store.ReadSection<TimetableStoreData>(SectionKey, JsonOptions) ?? new TimetableStoreData();
            return data.FindGmWinners.Select(w => new FindGmWinnerEntry
            {
                CharacterName = w.CharacterName,
                MapName = w.MapName,
                Coins = w.Coins,
                CoinType = w.CoinType,
                Zen = w.Zen,
                WonAtUtc = DateTime.SpecifyKind(w.WonAtUtc, DateTimeKind.Utc),
            }).ToList();
        }
    }
}
