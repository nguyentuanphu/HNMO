// <copyright file="MissionStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Missions;

using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;

/// <summary>
/// The progress of a mission in its current period.
/// </summary>
public sealed class MissionProgressState
{
    /// <summary>Gets or sets the count so far (at most the target).</summary>
    public int Value { get; set; }

    /// <summary>Gets or sets a value indicating whether the reward was claimed.</summary>
    public bool Claimed { get; set; }
}

/// <summary>
/// The mission progress of a character.
/// </summary>
public sealed class MissionCharacterState
{
    /// <summary>Gets or sets the character name (for the logs and the admin panel).</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the login name of its account.</summary>
    public string? Account { get; set; }

    /// <summary>Gets or sets the game day of <see cref="Daily"/>.</summary>
    public string DailyKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the game week (its Monday) of <see cref="Weekly"/>.</summary>
    public string WeeklyKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the daily progress by mission id.</summary>
    public Dictionary<string, MissionProgressState> Daily { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the weekly progress by mission id.</summary>
    public Dictionary<string, MissionProgressState> Weekly { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the days of the streak, counted up to <see cref="StreakLastDay"/>.</summary>
    public int StreakCount { get; set; }

    /// <summary>Gets or sets the last game day which counted for the streak.</summary>
    public string? StreakLastDay { get; set; }

    /// <summary>Gets or sets the game day of a streak bonus which wasn't claimed yet.</summary>
    public string? StreakBonusDay { get; set; }

    /// <summary>Gets or sets the levels whose milestone coins were given.</summary>
    public List<int> MilestonesRewarded { get; set; } = new();

    /// <summary>Gets or sets the resets whose coins were given; -1 until the character was seen.</summary>
    public int ResetsRewarded { get; set; } = -1;
}

/// <summary>
/// Earned coins which didn't fit under the daily cap; they're given on a later day.
/// </summary>
public sealed class OwedCoinsState
{
    /// <summary>Gets or sets the grant reference.</summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>Gets or sets the reason.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Gets or sets the amount.</summary>
    public int Amount { get; set; }

    /// <summary>Gets or sets the coin type.</summary>
    public CashShopCoinType CoinType { get; set; }
}

/// <summary>
/// The earned coins of an account.
/// </summary>
public sealed class MissionAccountState
{
    /// <summary>Gets or sets the game day of the daily counters.</summary>
    public string DayKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the earned coins of the day, over all capped sources.</summary>
    public int EarnedToday { get; set; }

    /// <summary>Gets or sets the counted seconds in the game of the day (auto play counts half).</summary>
    public int OnlineSecondsToday { get; set; }

    /// <summary>Gets or sets the full hours which were paid today.</summary>
    public int OnlineHoursPaidToday { get; set; }

    /// <summary>Gets or sets the coins for the online time of the day.</summary>
    public int OnlineCoinsToday { get; set; }

    /// <summary>Gets or sets the month of <see cref="CalendarDays"/>.</summary>
    public string CalendarMonth { get; set; } = string.Empty;

    /// <summary>Gets or sets the claimed days of the login calendar in its month.</summary>
    public int CalendarDays { get; set; }

    /// <summary>Gets or sets the game day of the last claim of the login calendar.</summary>
    public string? CalendarLastDay { get; set; }

    /// <summary>Gets or sets the account's own invite code.</summary>
    public string? InviteCode { get; set; }

    /// <summary>Gets or sets the login name of the account which invited this one.</summary>
    public string? InvitedBy { get; set; }

    /// <summary>Gets or sets a value indicating whether the invite reward was handled.</summary>
    public bool InviteRewarded { get; set; }

    /// <summary>Gets or sets the network address of the last login.</summary>
    public string? LastAddress { get; set; }

    /// <summary>Gets or sets the earned coins which wait for room under the daily cap.</summary>
    public List<OwedCoinsState> Owed { get; set; } = new();
}

/// <summary>
/// The saved data of the missions and earned coins, in a JSON file in the save folder (no database table):
/// the progress per character and the earned coins per account.
/// </summary>
public sealed class MissionStoreData
{
    /// <summary>Gets or sets the characters by their id.</summary>
    public Dictionary<string, MissionCharacterState> Characters { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the accounts by their login name.</summary>
    public Dictionary<string, MissionAccountState> Accounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Keeps the <see cref="MissionStoreData"/>; with a file, it outlasts a restart of the server.
/// Changes are saved by <see cref="SaveIfChanged"/> (every minute and after claims), so a crash loses at most
/// a minute of counting.
/// </summary>
public sealed class MissionStore
{
    private const string SectionKey = "missions";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly HnmoStateStore? _stateStore;
    private bool _changed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MissionStore"/> class.
    /// </summary>
    /// <param name="filePath">The file; <c>null</c> keeps the data in memory only.</param>
    public MissionStore(string? filePath = null)
    {
        this._stateStore = HnmoStateStore.Open(filePath, SectionKey);
        this.Data = this.Load();
    }

    /// <summary>Gets the lock which every access holds.</summary>
    public object Lock { get; } = new();

    /// <summary>Gets the file, if any.</summary>
    public string? FilePath => this._stateStore?.FilePath;

    /// <summary>Gets the data.</summary>
    public MissionStoreData Data { get; }

    /// <summary>
    /// Gets the state of a character, created when it's new.
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <returns>The state.</returns>
    public MissionCharacterState Character(string characterId)
    {
        if (!this.Data.Characters.TryGetValue(characterId, out var state))
        {
            state = new MissionCharacterState();
            this.Data.Characters[characterId] = state;
            this._changed = true;
        }

        return state;
    }

    /// <summary>
    /// Gets the state of an account, created when it's new.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <returns>The state.</returns>
    public MissionAccountState Account(string loginName)
    {
        if (!this.Data.Accounts.TryGetValue(loginName, out var state))
        {
            state = new MissionAccountState();
            this.Data.Accounts[loginName] = state;
            this._changed = true;
        }

        return state;
    }

    /// <summary>
    /// Finds the account with an invite code.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <returns>The login name, or <c>null</c>.</returns>
    public string? FindByInviteCode(string code) =>
        this.Data.Accounts.FirstOrDefault(a => string.Equals(a.Value.InviteCode, code, StringComparison.OrdinalIgnoreCase)).Key;

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

        try
        {
            this._stateStore.WriteSection(SectionKey, this.Data, JsonOptions);
            this._changed = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // It's saved with the next change.
        }
    }

    private MissionStoreData Load()
    {
        if (this._stateStore is null)
        {
            return new MissionStoreData();
        }

        var data = this._stateStore.ReadSection<MissionStoreData>(SectionKey, JsonOptions) ?? new MissionStoreData();

        // The dictionaries are read with the default comparer; put them back into the intended ones.
        data.Characters = new Dictionary<string, MissionCharacterState>(data.Characters, StringComparer.Ordinal);
        data.Accounts = new Dictionary<string, MissionAccountState>(data.Accounts, StringComparer.OrdinalIgnoreCase);
        return data;
    }
}
