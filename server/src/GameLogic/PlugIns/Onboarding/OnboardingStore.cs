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
/// The guide of a character.
/// </summary>
public sealed class OnboardingCharacterState
{
    /// <summary>Gets or sets the character name (for the logs).</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the login name of its account.</summary>
    public string? Account { get; set; }

    /// <summary>Gets or sets the status.</summary>
    public OnboardingStatus Status { get; set; }

    /// <summary>Gets or sets when it was first seen.</summary>
    public DateTime FirstSeenUtc { get; set; }

    /// <summary>Gets or sets when the guide ended (done or skipped).</summary>
    public DateTime? EndedUtc { get; set; }

    /// <summary>Gets or sets the ids of the steps which are done (or skipped, when optional).</summary>
    public List<string> Completed { get; set; } = new();

    /// <summary>Gets or sets the ids of the optional steps the player skipped (no reward).</summary>
    public List<string> SkippedSteps { get; set; } = new();

    /// <summary>Gets or sets the id of the step whose <see cref="Value"/> and <see cref="Baseline"/> these are.</summary>
    public string? StepId { get; set; }

    /// <summary>Gets or sets the count of the current step.</summary>
    public int Value { get; set; }

    /// <summary>Gets or sets the stat points or skills when the current step started (for the steps which count new ones).</summary>
    public int? Baseline { get; set; }

    /// <summary>Gets or sets a value indicating whether the character was created while the plug-in ran (it gets the kit's stat points then).</summary>
    public bool CreatedHere { get; set; }

    /// <summary>Gets or sets a value indicating whether the starter kit is this character's (given, or waiting for the first login).</summary>
    public bool KitReserved { get; set; }

    /// <summary>Gets or sets a value indicating whether the starter kit was handed over (it may still wait for room in the bag).</summary>
    public bool KitGiven { get; set; }

    /// <summary>Gets or sets a value indicating whether the player was told about the kit.</summary>
    public bool KitShown { get; set; }
}

/// <summary>
/// The starter kit of an account (when it's given once per account).
/// </summary>
public sealed class OnboardingAccountState
{
    /// <summary>Gets or sets the id of the character which got the account's kit.</summary>
    public string? KitCharacter { get; set; }
}

/// <summary>
/// A reward which waits to be given: its zen, items, stat points and buff need the character in the game with room in
/// the bag; its coins are given at once (to the account).
/// </summary>
public sealed class OnboardingOwedReward
{
    /// <summary>Gets or sets the reference; it makes the reward count once.</summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>Gets or sets what it's for: "step", "final" or "kit".</summary>
    public string Kind { get; set; } = "step";

    /// <summary>Gets or sets the step id, for a step.</summary>
    public string? StepId { get; set; }

    /// <summary>Gets or sets the English title (the step's or "Starter kit").</summary>
    public string TitleEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese title.</summary>
    public string TitleVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the reason (for the coins history and the logs).</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Gets or sets the login name of the account.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Gets or sets the zen.</summary>
    public int Zen { get; set; }

    /// <summary>Gets or sets the items.</summary>
    public List<StarterKitItem> Items { get; set; } = new();

    /// <summary>Gets or sets the coins.</summary>
    public int Coins { get; set; }

    /// <summary>Gets or sets the coin type.</summary>
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets or sets a value indicating whether the coins were given.</summary>
    public bool CoinsGiven { get; set; }

    /// <summary>Gets or sets the minutes of the EXP buff (0: none).</summary>
    public int BuffMinutes { get; set; }

    /// <summary>Gets or sets the item group whose effect is the buff.</summary>
    public short BuffItemGroup { get; set; }

    /// <summary>Gets or sets the item number whose effect is the buff.</summary>
    public short BuffItemNumber { get; set; }

    /// <summary>Gets or sets when it was earned.</summary>
    public DateTime EarnedAtUtc { get; set; }

    /// <summary>Gets a value indicating whether something needs the character in the game.</summary>
    [JsonIgnore]
    public bool HasGoods => this.Zen > 0 || this.BuffMinutes > 0 || this.Items.Any(i => i.Count > 0);
}

/// <summary>
/// The saved data of the new-player guide, in a JSON file in the save folder (no database table).
/// </summary>
public sealed class OnboardingStoreData
{
    /// <summary>Gets or sets when the plug-in first ran with this file; characters created after it are new.</summary>
    public DateTime ActivatedAtUtc { get; set; }

    /// <summary>Gets or sets the characters by their id.</summary>
    public Dictionary<string, OnboardingCharacterState> Characters { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the accounts by their login name.</summary>
    public Dictionary<string, OnboardingAccountState> Accounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets the waiting rewards by character id.</summary>
    public Dictionary<string, List<OnboardingOwedReward>> Owed { get; set; } = new(StringComparer.Ordinal);
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
