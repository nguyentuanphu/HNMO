// <copyright file="GiftCodeStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GiftCodes;

using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.Persistence;

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
    private static readonly ConcurrentDictionary<string, Lazy<GiftCodeStore>> Shared = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

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
        if (string.IsNullOrEmpty(this.Data.IdentitySalt))
        {
            if (this.Data.Claims.Values.Any(claims => claims.Any(c => c.StartsWith("n:", StringComparison.Ordinal) || c.StartsWith("d:", StringComparison.Ordinal))))
            {
                throw new InvalidDataException("Gift-code identity claims cannot be opened without their original salt.");
            }

            this.Data.IdentitySalt = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            this.Changed();
            this.SaveIfChanged();
            if (this._stateStore is not null && this._changed)
            {
                throw new IOException("The gift-code identity salt could not be saved.");
            }
        }
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
        var path = string.IsNullOrWhiteSpace(fileName) ? string.Empty : HnmoStateStore.ResolveStateFilePath(Path.GetFullPath(fileName.Trim(), AppContext.BaseDirectory))!;

        // GetOrAdd may run competing factories; only the winning lazy value may create and persist a salt.
        return Shared.GetOrAdd(path, p => new Lazy<GiftCodeStore>(() => new GiftCodeStore(p.Length == 0 ? null : p))).Value;
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
        IdentitySalt = this.Data.IdentitySalt,
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
                    IdentitySalt = data.IdentitySalt,
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
