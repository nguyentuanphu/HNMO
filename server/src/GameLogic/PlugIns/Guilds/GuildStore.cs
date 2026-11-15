// <copyright file="GuildStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Guilds;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// The experience and level of a guild.
/// </summary>
public sealed class GuildProgress
{
    /// <summary>Gets or sets the guild's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the guild experience in total.</summary>
    public long Xp { get; set; }

    /// <summary>Gets or sets the level.</summary>
    public int Level { get; set; } = 1;

    /// <summary>Gets or sets the ladder points of the current week (its guild experience plus bonuses).</summary>
    public long WeekXp { get; set; }

    /// <summary>Gets or sets when the week's points were last raised (an earlier time wins a tie).</summary>
    public DateTime WeekUpdatedAtUtc { get; set; }

    /// <summary>Gets or sets the week's guild experience by member name.</summary>
    public Dictionary<string, long> WeekMembers { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets when the guild reached its level.</summary>
    public DateTime? LevelUpAtUtc { get; set; }
}

/// <summary>
/// A line of a finished week's ladder.
/// </summary>
public sealed class GuildLadderLine
{
    /// <summary>Gets or sets the rank.</summary>
    public int Rank { get; set; }

    /// <summary>Gets or sets the persistent guild id.</summary>
    public Guid GuildId { get; set; }

    /// <summary>Gets or sets the guild's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the ladder points.</summary>
    public long Xp { get; set; }

    /// <summary>Gets or sets the coins each member got.</summary>
    public int CoinsPerMember { get; set; }

    /// <summary>Gets or sets the coin type.</summary>
    public HNMO.DataModel.Configuration.CashShopCoinType CoinType { get; set; }

    /// <summary>Gets or sets the members who were paid.</summary>
    public int PaidMembers { get; set; }
}

/// <summary>
/// A finished week of the guild ladder.
/// </summary>
public sealed class GuildWeekResult
{
    /// <summary>Gets or sets the week (its Monday).</summary>
    public string Week { get; set; } = string.Empty;

    /// <summary>Gets or sets the ladder, best first (the top 10).</summary>
    public List<GuildLadderLine> Lines { get; set; } = new();

    /// <summary>Gets or sets the references which were paid already (so a retry pays nobody twice).</summary>
    public List<string> PaidReferences { get; set; } = new();
}

/// <summary>
/// A castle siege won by a guild.
/// </summary>
public sealed class GuildSiegeResult
{
    /// <summary>Gets or sets when it ended.</summary>
    public DateTime AtUtc { get; set; }

    /// <summary>Gets or sets the persistent guild id.</summary>
    public Guid GuildId { get; set; }

    /// <summary>Gets or sets the guild's name.</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// A ladder reward which waits for room in the bag (zen and items; the coins are given at once).
/// </summary>
public sealed class GuildOwedReward
{
    /// <summary>Gets or sets the reference; it makes the reward count once.</summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>Gets or sets the reason (logs).</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Gets or sets the week.</summary>
    public string Week { get; set; } = string.Empty;

    /// <summary>Gets or sets the rank.</summary>
    public int Rank { get; set; }

    /// <summary>Gets or sets the reward (zen and items).</summary>
    public MissionReward Reward { get; set; } = new();
}

/// <summary>
/// The saved state of the guild levels and the ladder, in a JSON file in the save folder (no database table).
/// </summary>
public sealed class GuildStoreData
{
    /// <summary>Gets or sets the progress by persistent guild id.</summary>
    public Dictionary<Guid, GuildProgress> Guilds { get; set; } = new();

    /// <summary>Gets or sets the current day.</summary>
    public string DayKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the capped guild experience earned today, by character id.</summary>
    public Dictionary<string, int> Today { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the current week (its Monday).</summary>
    public string WeekKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the last finished week.</summary>
    public GuildWeekResult? LastWeek { get; set; }

    /// <summary>Gets or sets the finished weeks which wait to be paid.</summary>
    public List<GuildWeekResult> PendingWeeks { get; set; } = new();

    /// <summary>Gets or sets the weeks which were paid.</summary>
    public List<string> PaidWeeks { get; set; } = new();

    /// <summary>Gets or sets the castle sieges, newest first.</summary>
    public List<GuildSiegeResult> Sieges { get; set; } = new();

    /// <summary>Gets or sets the rewards which wait for room in the bag, by character id.</summary>
    public Dictionary<string, List<GuildOwedReward>> Owed { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Keeps the <see cref="GuildStoreData"/>; with a file, it outlasts a restart of the server. All game servers, the
/// admin panel and the website of a process share one store per file (<see cref="Open"/>).
/// </summary>
public sealed class GuildStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private static readonly ConcurrentDictionary<string, GuildStore> Shared = new(StringComparer.Ordinal);

    private readonly string? _filePath;
    private bool _changed;

    /// <summary>
    /// Initializes a new instance of the <see cref="GuildStore"/> class.
    /// </summary>
    /// <param name="filePath">The file; <c>null</c> keeps the data in memory only.</param>
    public GuildStore(string? filePath = null)
    {
        this._filePath = filePath;
        this.Data = this.Load();
    }

    /// <summary>Gets the lock which every access holds.</summary>
    public object Lock { get; } = new();

    /// <summary>Gets the data.</summary>
    public GuildStoreData Data { get; }

    /// <summary>
    /// Gets the shared store of a configured file name (relative to the server's folder).
    /// </summary>
    /// <param name="fileName">The configured file name; empty for a store in memory.</param>
    /// <returns>The store.</returns>
    public static GuildStore Open(string? fileName)
    {
        var path = string.IsNullOrWhiteSpace(fileName) ? string.Empty : Path.GetFullPath(fileName.Trim(), AppContext.BaseDirectory);
        return Shared.GetOrAdd(path, p => new GuildStore(p.Length == 0 ? null : p));
    }

    /// <summary>Marks the data as changed.</summary>
    public void Changed() => this._changed = true;

    /// <summary>
    /// Saves the data when it changed. The caller holds <see cref="Lock"/>.
    /// </summary>
    public void SaveIfChanged()
    {
        if (!this._changed || this._filePath is null)
        {
            this._changed = false;
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(this._filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporary = this._filePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(this.Data, JsonOptions));
            File.Move(temporary, this._filePath, true);
            this._changed = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // It's saved with the next change.
        }
    }

    private GuildStoreData Load()
    {
        if (this._filePath is null || !File.Exists(this._filePath))
        {
            return new GuildStoreData();
        }

        try
        {
            var data = JsonSerializer.Deserialize<GuildStoreData>(File.ReadAllText(this._filePath), JsonOptions) ?? new GuildStoreData();
            data.Today = new Dictionary<string, int>(data.Today, StringComparer.Ordinal);
            data.Owed = new Dictionary<string, List<GuildOwedReward>>(data.Owed, StringComparer.Ordinal);
            foreach (var guild in data.Guilds.Values)
            {
                guild.WeekMembers = new Dictionary<string, long>(guild.WeekMembers, StringComparer.Ordinal);
            }

            return data;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            try
            {
                File.Copy(this._filePath, this._filePath + ".broken", true);
            }
            catch (Exception copyError) when (copyError is IOException or UnauthorizedAccessException)
            {
                // nothing more to do
            }

            return new GuildStoreData();
        }
    }
}
