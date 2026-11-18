// <copyright file="GiftCodeStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GiftCodes;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.Persistence;

/// <summary>
/// A redemption of a gift code (the admin panel's log).
/// </summary>
public sealed class GiftCodeLogEntry
{
    /// <summary>Gets or sets when.</summary>
    public DateTime AtUtc { get; set; }

    /// <summary>Gets or sets the code's id.</summary>
    public string CodeId { get; set; } = string.Empty;

    /// <summary>Gets or sets the code which was entered (normalized).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the account's login name.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Gets or sets the character's id.</summary>
    public string CharacterId { get; set; } = string.Empty;

    /// <summary>Gets or sets the character's name.</summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>Gets or sets where it was entered: "game" or "website".</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Gets or sets the reference which makes the reward count once.</summary>
    public string Reference { get; set; } = string.Empty;
}

/// <summary>
/// Coins of a redemption which weren't given yet (they're given once, by their reference).
/// </summary>
public sealed class GiftCodePendingCoins
{
    /// <summary>Gets or sets the reference.</summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>Gets or sets the account's login name.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Gets or sets the coin type.</summary>
    public CashShopCoinType CoinType { get; set; }

    /// <summary>Gets or sets the amount.</summary>
    public int Amount { get; set; }

    /// <summary>Gets or sets the reason in the coins history.</summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Zen and items of a redemption which wait for the character to be in the game with room in the bag.
/// </summary>
public sealed class GiftCodeOwedReward
{
    /// <summary>Gets or sets the reference; it makes the reward count once.</summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>Gets or sets the code's id.</summary>
    public string CodeId { get; set; } = string.Empty;

    /// <summary>Gets or sets the code which was entered.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the zen.</summary>
    public int Zen { get; set; }

    /// <summary>Gets or sets the items.</summary>
    public List<GiftCodeItem> Items { get; set; } = new();
}

/// <summary>
/// The saved state of the gift codes, in a JSON file in the save folder (no database table).
/// </summary>
public sealed class GiftCodeStoreData
{
    /// <summary>Gets or sets the codes.</summary>
    public List<GiftCodeDefinition> Codes { get; set; } = new();

    /// <summary>Gets or sets how often each code was used, by code id.</summary>
    public Dictionary<string, int> Uses { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets who used each code ("a:login" and "c:character id"), by code id.</summary>
    public Dictionary<string, HashSet<string>> Claims { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the batch codes which were used (normalized), with the reference of their redemption.</summary>
    public Dictionary<string, string> UsedBatchCodes { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the redemptions, newest first.</summary>
    public List<GiftCodeLogEntry> Log { get; set; } = new();

    /// <summary>Gets or sets the coins which weren't given yet.</summary>
    public List<GiftCodePendingCoins> PendingCoins { get; set; } = new();

    /// <summary>Gets or sets the zen and items waiting for the character, by character id.</summary>
    public Dictionary<string, List<GiftCodeOwedReward>> Owed { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the times of wrong codes, by login name (lower case).</summary>
    public Dictionary<string, List<DateTime>> WrongTries { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Keeps the <see cref="GiftCodeStoreData"/>; with a file, it outlasts a restart of the server. All game servers, the
/// admin panel and the website of a process share one store per file (<see cref="Open"/>).
/// </summary>
public sealed class GiftCodeStore
{
    private const string FeatureClaims = "giftCodes.claims";
    private const string FeatureOwed = "giftCodes.owed";
    private const string FeatureWrongTries = "giftCodes.wrongTries";
    private const string SectionKey = "giftCodes";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private static readonly ConcurrentDictionary<string, GiftCodeStore> Shared = new(StringComparer.Ordinal);

    private readonly HnmoStateStore? _stateStore;
    private readonly IHnmoPlayerStateRowBackend _rowBackend;
    private readonly HnmoFeatureRowSync<HashSet<string>> _claimsSync = new();
    private readonly HnmoFeatureRowSync<List<GiftCodeOwedReward>> _owedSync = new();
    private readonly HnmoFeatureRowSync<List<DateTime>> _wrongTriesSync = new();
    private bool _changed;

    /// <summary>
    /// Initializes a new instance of the <see cref="GiftCodeStore"/> class.
    /// </summary>
    /// <param name="filePath">The file; <c>null</c> keeps the data in memory only.</param>
    public GiftCodeStore(string? filePath = null)
    {
        this._stateStore = HnmoStateStore.Open(filePath, SectionKey);
        this._rowBackend = HnmoPlayerStateRowStoreRegistry.GetBackend(this._stateStore?.FilePath);
        this.Data = this.Load();
    }

    /// <summary>Gets the lock which every access holds.</summary>
    public object Lock { get; } = new();

    /// <summary>Gets the data.</summary>
    public GiftCodeStoreData Data { get; }

    /// <summary>Gets a number which grows with every change of the codes (for caches of the code index).</summary>
    public int CodesVersion { get; private set; }

    /// <summary>
    /// Gets the shared store of a configured file name (relative to the server's folder).
    /// </summary>
    /// <param name="fileName">The configured file name; empty for a store in memory.</param>
    /// <returns>The store.</returns>
    public static GiftCodeStore Open(string? fileName)
    {
        var path = string.IsNullOrWhiteSpace(fileName) ? string.Empty : Path.GetFullPath(fileName.Trim(), AppContext.BaseDirectory);
        return Shared.GetOrAdd(path, p => new GiftCodeStore(p.Length == 0 ? null : p));
    }

    /// <summary>Marks the data as changed.</summary>
    public void Changed() => this._changed = true;

    /// <summary>Marks the codes as changed. The caller holds <see cref="Lock"/>.</summary>
    public void CodesChanged()
    {
        this._changed = true;
        this.CodesVersion++;
    }

    /// <summary>
    /// Saves the data when it changed. The caller holds <see cref="Lock"/>.
    /// </summary>
    public void SaveIfChanged()
    {
        if (!this._changed || this._stateStore is null)
        {
            this._changed = false;
            return;
        }

        DateTime? ClaimExpiry(string codeId, HashSet<string> _)
        {
            var def = this.Data.Codes.FirstOrDefault(c => string.Equals(c.Id, codeId, StringComparison.Ordinal));
            return ParseEndUtc(def?.End);
        }

        this._claimsSync.SaveChanges(this._rowBackend, FeatureClaims, this.Data.Claims, JsonOptions, ClaimExpiry, useDirtySetOnly: true);
        this._owedSync.SaveChanges(this._rowBackend, FeatureOwed, this.Data.Owed, JsonOptions, useDirtySetOnly: true);
        this._wrongTriesSync.SaveChanges(this._rowBackend, FeatureWrongTries, this.Data.WrongTries, JsonOptions, useDirtySetOnly: true);

        try
        {
            this._stateStore.WriteSection(SectionKey, this.BuildServerOnlyData(), JsonOptions);
            this._changed = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // It's saved with the next change.
        }
    }

    private static DateTime? ParseEndUtc(string? endText)
    {
        if (string.IsNullOrWhiteSpace(endText)
            || HNMO.GameLogic.PlugIns.TopRaces.TopRaceEngine.ParseLocal(endText) is not { } local)
        {
            return null;
        }

        return HNMO.GameLogic.PlugIns.TopRaces.TopRaceEngine.LocalToUtc(
            local,
            new HNMO.GameLogic.PlugIns.Missions.MissionClock("Australia/Sydney", 0).TimeZone);
    }

    private GiftCodeStoreData BuildServerOnlyData() => new()
    {
        Codes = this.Data.Codes,
        Uses = this.Data.Uses,
        UsedBatchCodes = this.Data.UsedBatchCodes,
        Log = this.Data.Log,
        PendingCoins = this.Data.PendingCoins,
    };

    private GiftCodeStoreData Load()
    {
        if (this._stateStore is null)
        {
            return new GiftCodeStoreData();
        }

        var data = this._stateStore.ReadSection<GiftCodeStoreData>(SectionKey, JsonOptions) ?? new GiftCodeStoreData();
        data.Uses = new Dictionary<string, int>(data.Uses, StringComparer.Ordinal);
        data.Claims = data.Claims.ToDictionary(p => p.Key, p => new HashSet<string>(p.Value, StringComparer.Ordinal), StringComparer.Ordinal);
        data.UsedBatchCodes = new Dictionary<string, string>(data.UsedBatchCodes, StringComparer.Ordinal);
        data.Owed = new Dictionary<string, List<GiftCodeOwedReward>>(data.Owed, StringComparer.Ordinal);
        data.WrongTries = new Dictionary<string, List<DateTime>>(data.WrongTries, StringComparer.Ordinal);

        DateTime? ClaimExpiry(string codeId, HashSet<string> _)
        {
            var def = data.Codes.FirstOrDefault(c => string.Equals(c.Id, codeId, StringComparison.Ordinal));
            return ParseEndUtc(def?.End);
        }

        var m1 = this._claimsSync.LoadOrMigrate(this._rowBackend, FeatureClaims, data.Claims, JsonOptions, ClaimExpiry);
        var m2 = this._owedSync.LoadOrMigrate(this._rowBackend, FeatureOwed, data.Owed, JsonOptions);
        var m3 = this._wrongTriesSync.LoadOrMigrate(this._rowBackend, FeatureWrongTries, data.WrongTries, JsonOptions);
        data.Claims = new Dictionary<string, HashSet<string>>(data.Claims, StringComparer.Ordinal);
        if (m1 || m2 || m3)
        {
            this._stateStore.WriteSection(
                SectionKey,
                new GiftCodeStoreData
                {
                    Codes = data.Codes,
                    Uses = data.Uses,
                    UsedBatchCodes = data.UsedBatchCodes,
                    Log = data.Log,
                    PendingCoins = data.PendingCoins,
                },
                JsonOptions);
        }

        return data;
    }
}
