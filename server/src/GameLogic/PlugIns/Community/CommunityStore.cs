// <copyright file="CommunityStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Community;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// The state of a Zalo claim.
/// </summary>
public enum ZaloClaimStatus
{
    /// <summary>No claim.</summary>
    None,

    /// <summary>The player asked; the admin hasn't decided yet.</summary>
    Pending,

    /// <summary>The admin approved it; the coins are paid (or paid on the next tick).</summary>
    Approved,

    /// <summary>The admin rejected it; the player can ask again.</summary>
    Rejected,
}

/// <summary>
/// What an account got from the community rewards.
/// </summary>
public sealed class CommunityAccountState
{
    /// <summary>Gets or sets the login name, as it was written.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Gets or sets when the Discord coins were paid.</summary>
    public DateTime? DiscordPaidAtUtc { get; set; }

    /// <summary>Gets or sets the Discord user the coins were paid for.</summary>
    public string? DiscordUserId { get; set; }

    /// <summary>Gets or sets the state of the Zalo claim.</summary>
    public ZaloClaimStatus ZaloStatus { get; set; }

    /// <summary>Gets or sets the code the player shows in the Zalo group (or sends to the admin).</summary>
    public string? ZaloCode { get; set; }

    /// <summary>Gets or sets the name the player has in Zalo.</summary>
    public string? ZaloName { get; set; }

    /// <summary>Gets or sets the character which asked.</summary>
    public string? ZaloCharacter { get; set; }

    /// <summary>Gets or sets when the player asked.</summary>
    public DateTime? ZaloRequestedAtUtc { get; set; }

    /// <summary>Gets or sets when the admin decided.</summary>
    public DateTime? ZaloDecidedAtUtc { get; set; }

    /// <summary>Gets or sets when the Zalo coins were paid.</summary>
    public DateTime? ZaloPaidAtUtc { get; set; }
}

/// <summary>
/// A reward of a community event which a character got (or waits for).
/// </summary>
public sealed class CommunityEventEntry
{
    /// <summary>Gets or sets the event id.</summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>Gets or sets the character name.</summary>
    public string Character { get; set; } = string.Empty;

    /// <summary>Gets or sets the login name.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the character was a listed winner (else it entered the code).</summary>
    public bool Winner { get; set; }

    /// <summary>Gets or sets when it was recorded.</summary>
    public DateTime AtUtc { get; set; }

    /// <summary>Gets or sets a value indicating whether the coins were paid.</summary>
    public bool CoinsPaid { get; set; }
}

/// <summary>
/// Zen and items of an event which wait for the character to be online with room in the bag.
/// </summary>
public sealed class CommunityOwedReward
{
    /// <summary>Gets or sets the reference; it makes the reward count once.</summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>Gets or sets the event id.</summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>Gets or sets the English title of the event.</summary>
    public string TitleEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese title of the event.</summary>
    public string TitleVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the reward (zen and items).</summary>
    public MissionReward Reward { get; set; } = new();
}

/// <summary>
/// An automatic post (a highlight): what happened, in both languages.
/// </summary>
public sealed class CommunityHighlight
{
    /// <summary>Gets or sets when it happened.</summary>
    public DateTime AtUtc { get; set; }

    /// <summary>Gets or sets the kind.</summary>
    public CommunityPostKind Kind { get; set; }

    /// <summary>Gets or sets the English text.</summary>
    public string TextEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese text.</summary>
    public string TextVi { get; set; } = string.Empty;
}

/// <summary>
/// The saved state of the community rewards, events and highlights, in a JSON file in the save folder (no database table).
/// </summary>
public sealed class CommunityStoreData
{
    /// <summary>Gets or sets the accounts, by login name in lower case.</summary>
    public Dictionary<string, CommunityAccountState> Accounts { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the Discord users whose coins were paid, with the login name (lower case).</summary>
    public Dictionary<string, string> PaidDiscordUsers { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the event entries, by "event id|character name (lower case)".</summary>
    public Dictionary<string, CommunityEventEntry> Entries { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the code entries, by "event id|login (lower case)" (a code counts once per account).</summary>
    public Dictionary<string, string> CodeAccounts { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the rewards which wait for the character, by character name (lower case).</summary>
    public Dictionary<string, List<CommunityOwedReward>> Owed { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the highlights, newest first.</summary>
    public List<CommunityHighlight> Highlights { get; set; } = new();
}

/// <summary>
/// Keeps the <see cref="CommunityStoreData"/>; with a file, it outlasts a restart of the server. All game servers, the
/// admin panel and the website of a process share one store per file (<see cref="Open"/>).
/// </summary>
public sealed class CommunityStore
{
    private const string SectionKey = "community";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private static readonly ConcurrentDictionary<string, CommunityStore> Shared = new(StringComparer.Ordinal);

    private readonly HnmoStateStore? _stateStore;
    private bool _changed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommunityStore"/> class.
    /// </summary>
    /// <param name="filePath">The file; <c>null</c> keeps the data in memory only.</param>
    public CommunityStore(string? filePath = null)
    {
        this._stateStore = HnmoStateStore.Open(filePath, SectionKey);
        this.Data = this.Load();
    }

    /// <summary>Gets the lock which every access holds.</summary>
    public object Lock { get; } = new();

    /// <summary>Gets the data.</summary>
    public CommunityStoreData Data { get; }

    /// <summary>
    /// Gets the shared store of a configured file name (relative to the server's folder).
    /// </summary>
    /// <param name="fileName">The configured file name; empty for a store in memory.</param>
    /// <returns>The store.</returns>
    public static CommunityStore Open(string? fileName)
    {
        var path = string.IsNullOrWhiteSpace(fileName) ? string.Empty : Path.GetFullPath(fileName.Trim(), AppContext.BaseDirectory);
        return Shared.GetOrAdd(path, p => new CommunityStore(p.Length == 0 ? null : p));
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

    private CommunityStoreData Load()
    {
        if (this._stateStore is null)
        {
            return new CommunityStoreData();
        }

        var data = this._stateStore.ReadSection<CommunityStoreData>(SectionKey, JsonOptions) ?? new CommunityStoreData();
        data.Accounts = new Dictionary<string, CommunityAccountState>(data.Accounts, StringComparer.Ordinal);
        data.PaidDiscordUsers = new Dictionary<string, string>(data.PaidDiscordUsers, StringComparer.Ordinal);
        data.Entries = new Dictionary<string, CommunityEventEntry>(data.Entries, StringComparer.Ordinal);
        data.CodeAccounts = new Dictionary<string, string>(data.CodeAccounts, StringComparer.Ordinal);
        data.Owed = new Dictionary<string, List<CommunityOwedReward>>(data.Owed, StringComparer.Ordinal);
        return data;
    }
}
