// <copyright file="LuckyNumberStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.LuckyNumber;

using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;

/// <summary>
/// A single player pick recorded for a daily draw round.
/// </summary>
public sealed class LuckyNumberPickEntry
{
    /// <summary>Gets or sets the normalized account login name.</summary>
    public string LoginName { get; set; } = string.Empty;

    /// <summary>Gets or sets the character name (or login name when picked from website).</summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>Gets or sets the chosen number.</summary>
    public int Number { get; set; }

    /// <summary>Gets or sets the UTC pick timestamp.</summary>
    public DateTime PickedAtUtc { get; set; }

    /// <summary>Gets or sets the source (<c>game</c> or <c>website</c>).</summary>
    public string Source { get; set; } = "game";
}

/// <summary>
/// A winning pick from a completed daily draw.
/// </summary>
public sealed class LuckyNumberWinnerEntry
{
    /// <summary>Gets or sets the winning account login name.</summary>
    public string LoginName { get; set; } = string.Empty;

    /// <summary>Gets or sets the character display name.</summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>Gets or sets the picked number.</summary>
    public int Number { get; set; }

    /// <summary>Gets or sets the prize tier (<c>exact</c> or <c>lastDigit</c>).</summary>
    public string Tier { get; set; } = "exact";

    /// <summary>Gets or sets coins awarded.</summary>
    public int Coins { get; set; }

    /// <summary>Gets or sets coin currency.</summary>
    public CashShopCoinType CoinType { get; set; }

    /// <summary>Gets or sets jewel count owed/granted.</summary>
    public int JewelCount { get; set; }

    /// <summary>Gets or sets the jewel item group.</summary>
    public int JewelGroup { get; set; }

    /// <summary>Gets or sets the jewel item number.</summary>
    public int JewelNumber { get; set; }

    /// <summary>Gets or sets a value indicating whether coins have been queued/granted.</summary>
    public bool CoinsGranted { get; set; }
}

/// <summary>
/// A daily lucky number round (identified by <c>yyyy-MM-dd</c> draw date in Sydney/configured time zone).
/// </summary>
public sealed class LuckyNumberRound
{
    /// <summary>Gets or sets the draw date key (<c>yyyy-MM-dd</c>).</summary>
    public string DateKey { get; set; } = string.Empty;

    /// <summary>Gets or sets all picks submitted for this round.</summary>
    public List<LuckyNumberPickEntry> Picks { get; set; } = [];

    /// <summary>Gets or sets the drawn winning number, or <c>null</c> until drawn.</summary>
    public int? DrawnNumber { get; set; }

    /// <summary>Gets or sets the UTC draw timestamp.</summary>
    public DateTime? DrawnAtUtc { get; set; }

    /// <summary>Gets or sets the winners matched during the draw.</summary>
    public List<LuckyNumberWinnerEntry> Winners { get; set; } = [];
}

/// <summary>
/// Jewels waiting for an account to claim inside the game.
/// </summary>
public sealed class LuckyNumberOwedReward
{
    /// <summary>Gets or sets the unique claim ID.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the round date key.</summary>
    public string DateKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the tier (<c>exact</c> or <c>lastDigit</c>).</summary>
    public string Tier { get; set; } = "exact";

    /// <summary>Gets or sets the item group.</summary>
    public int Group { get; set; }

    /// <summary>Gets or sets the item number.</summary>
    public int Number { get; set; }

    /// <summary>Gets or sets the item quantity.</summary>
    public int Count { get; set; }
}

/// <summary>
/// Root store document stored in <see cref="HnmoStateStore"/> under key <c>luckyNumber</c>.
/// </summary>
public sealed class LuckyNumberStoreData
{
    /// <summary>Gets or sets recent daily rounds (newest first).</summary>
    public List<LuckyNumberRound> Rounds { get; set; } = [];

    /// <summary>Gets or sets unclaimed jewel rewards keyed by lowercase login name.</summary>
    public Dictionary<string, List<LuckyNumberOwedReward>> OwedByAccount { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

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
