// <copyright file="OnboardingStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Onboarding;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;
using HNMO.Persistence;

/// <summary>
/// Where a character is in the new-player guide.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<OnboardingStatus>))]
public enum OnboardingStatus
{
    /// <summary>The character wasn't new when it was first seen: no guide, no kit.</summary>
    NotEligible,

    /// <summary>The guide runs.</summary>
    Active,

    /// <summary>Every step is done (the final reward was earned).</summary>
    Done,

    /// <summary>The player skipped the guide.</summary>
    Skipped,
}

/// <summary>
/// Keeps the <see cref="OnboardingStoreData"/>; with a file, it outlasts a restart of the server. All game servers of a
/// process share one store per file (<see cref="Open"/>).
/// </summary>
public sealed class OnboardingStore
{
    private const string FeatureCharacters = "onboarding.char";
    private const string FeatureAccounts = "onboarding.account";
    private const string FeatureOwed = "onboarding.owed";
    private const string SectionKey = "onboarding";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private static readonly ConcurrentDictionary<string, OnboardingStore> Shared = new(StringComparer.Ordinal);

    private readonly HnmoStateStore? _stateStore;
    private readonly IHnmoPlayerStateRowBackend _rowBackend;
    private readonly HnmoFeatureRowSync<OnboardingCharacterState> _charSync = new();
    private readonly HnmoFeatureRowSync<OnboardingAccountState> _accSync = new();
    private readonly HnmoFeatureRowSync<List<OnboardingOwedReward>> _owedSync = new();
    private DateTime _savedActivatedAtUtc;
    private bool _changed;

    /// <summary>
    /// Initializes a new instance of the <see cref="OnboardingStore"/> class.
    /// </summary>
    /// <param name="filePath">The file; <c>null</c> keeps the data in memory only.</param>
    /// <param name="nowUtc">The time to keep as the activation time of a new store.</param>
    public OnboardingStore(string? filePath = null, DateTime? nowUtc = null)
    {
        this._stateStore = HnmoStateStore.Open(filePath, SectionKey);
        this._rowBackend = HnmoPlayerStateRowStoreRegistry.GetBackend(this._stateStore?.FilePath);
        this.Data = this.Load();
        if (this.Data.ActivatedAtUtc == default)
        {
            this.Data.ActivatedAtUtc = nowUtc ?? DateTime.UtcNow;
            this._changed = true;
        }
    }

    /// <summary>Gets the lock which every access holds.</summary>
    public object Lock { get; } = new();

    /// <summary>Gets the file, if any.</summary>
    public string? FilePath => this._stateStore?.FilePath;

    /// <summary>Gets the data.</summary>
    public OnboardingStoreData Data { get; }

    /// <summary>
    /// Gets the shared store of a configured file name (relative to the server's folder).
    /// </summary>
    /// <param name="fileName">The configured file name; empty for a store in memory.</param>
    /// <returns>The store.</returns>
    public static OnboardingStore Open(string? fileName)
    {
        var path = string.IsNullOrWhiteSpace(fileName) ? string.Empty : Path.GetFullPath(fileName.Trim(), AppContext.BaseDirectory);
        return Shared.GetOrAdd(path, p => new OnboardingStore(p.Length == 0 ? null : p));
    }

    /// <summary>Marks the data as changed.</summary>
    public void Changed() => this._changed = true;

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

        this._charSync.SaveChanges(this._rowBackend, FeatureCharacters, this.Data.Characters, JsonOptions, useDirtySetOnly: true);
        this._accSync.SaveChanges(this._rowBackend, FeatureAccounts, this.Data.Accounts, JsonOptions, useDirtySetOnly: true);
        this._owedSync.SaveChanges(this._rowBackend, FeatureOwed, this.Data.Owed, JsonOptions, useDirtySetOnly: true);
        if (this._savedActivatedAtUtc != this.Data.ActivatedAtUtc)
        {
            try
            {
                this._stateStore.WriteSection(SectionKey, new OnboardingStoreData { ActivatedAtUtc = this.Data.ActivatedAtUtc }, JsonOptions);
                this._savedActivatedAtUtc = this.Data.ActivatedAtUtc;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Saved with next change.
            }
        }

        this._changed = false;
    }

    private OnboardingStoreData Load()
    {
        if (this._stateStore is null)
        {
            return new OnboardingStoreData();
        }

        var data = this._stateStore.ReadSection<OnboardingStoreData>(SectionKey, JsonOptions) ?? new OnboardingStoreData();
        this._savedActivatedAtUtc = data.ActivatedAtUtc;
        data.Characters = new Dictionary<string, OnboardingCharacterState>(data.Characters, StringComparer.Ordinal);
        data.Accounts = new Dictionary<string, OnboardingAccountState>(data.Accounts, StringComparer.OrdinalIgnoreCase);
        data.Owed = new Dictionary<string, List<OnboardingOwedReward>>(data.Owed, StringComparer.Ordinal);

        var m1 = this._charSync.LoadOrMigrate(this._rowBackend, FeatureCharacters, data.Characters, JsonOptions);
        var m2 = this._accSync.LoadOrMigrate(this._rowBackend, FeatureAccounts, data.Accounts, JsonOptions);
        var m3 = this._owedSync.LoadOrMigrate(this._rowBackend, FeatureOwed, data.Owed, JsonOptions);
        if (m1 || m2 || m3)
        {
            this._stateStore.WriteSection(SectionKey, new OnboardingStoreData { ActivatedAtUtc = data.ActivatedAtUtc }, JsonOptions);
        }

        return data;
    }
}
